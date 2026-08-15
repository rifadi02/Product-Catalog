namespace Catalog.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ProductEndpointTests(CatalogApiFactory factory) : IAsyncLifetime
{
    private static readonly DateTime Baseline = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync() => await factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static CreateProductRequest NewProduct(
        string name = "Desk Lamp", string? description = "Warm light", decimal price = 24.99m) =>
        new() { Name = name, Description = description, Price = price };

    [DockerFact]
    public async Task The_catalogue_is_readable_without_a_token()
    {
        await factory.AddProductAsync(new ProductBuilder().Named("Public Item").Build());

        var page = await factory.CreateClient()
            .GetFromJsonAsync<PagedResponse<ProductDto>>("/api/v1/products");

        page!.Items.Should().ContainSingle(p => p.Name == "Public Item");
    }

    [DockerFact]
    public async Task Soft_deleted_products_are_invisible_everywhere()
    {
        var live = await factory.AddProductAsync(new ProductBuilder().Named("Live").Build());
        var dead = await factory.AddProductAsync(
            new ProductBuilder().Named("Discontinued").SoftDeleted().Build());

        var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<PagedResponse<ProductDto>>("/api/v1/products");
        page!.Items.Should().ContainSingle().Which.Id.Should().Be(live.Id);
        page.TotalCount.Should().Be(1, "TotalCount must reflect the filtered set, not the table");

        var byId = await client.GetAsync($"/api/v1/products/{dead.Id}");
        byId.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var search = await client.GetFromJsonAsync<PagedResponse<ProductDto>>(
            "/api/v1/products/search?name=Discontinued");
        search!.Items.Should().BeEmpty();
    }

    [DockerFact]
    public async Task An_empty_result_set_is_a_200_not_a_404()
    {
        var page = await factory.CreateClient()
            .GetFromJsonAsync<PagedResponse<ProductDto>>("/api/v1/products");

        page!.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
        page.TotalPages.Should().Be(0);
    }

    [DockerFact]
    public async Task PageSize_is_clamped_rather_than_rejected()
    {
        for (var i = 0; i < 3; i++)
            await factory.AddProductAsync(new ProductBuilder().Named($"Item {i}").Build());

        var page = await factory.CreateClient()
            .GetFromJsonAsync<PagedResponse<ProductDto>>("/api/v1/products?page=1&pageSize=100");

        page!.PageSize.Should().Be(100);
    }

    [DockerTheory]
    [InlineData("/api/v1/products?page=0")]
    [InlineData("/api/v1/products?pageSize=0")]
    [InlineData("/api/v1/products?pageSize=101")]
    [InlineData("/api/v1/products?sortBy=colour")]
    public async Task Bad_query_parameters_are_400_validation_failed(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Code.Should().Be("validation_failed");
    }

    [DockerFact]
    public async Task Sorting_is_tie_broken_by_id_so_paging_is_stable()
    {
        for (var i = 0; i < 6; i++)
            await factory.AddProductAsync(
                new ProductBuilder().Named($"Item {i}").Priced(10m).Build());

        var client = factory.CreateClient();

        var first = await client.GetFromJsonAsync<PagedResponse<ProductDto>>(
            "/api/v1/products?sortBy=Price&direction=Asc&page=1&pageSize=3");
        var second = await client.GetFromJsonAsync<PagedResponse<ProductDto>>(
            "/api/v1/products?sortBy=Price&direction=Asc&page=2&pageSize=3");

        var ids = first!.Items.Select(p => p.Id).Concat(second!.Items.Select(p => p.Id)).ToArray();

        ids.Should().OnlyHaveUniqueItems();
        ids.Should().BeInAscendingOrder();
    }

    /// <summary>
    /// Every sortable column in both directions. The whitelist in <c>ApplyOrdering</c> is a switch
    /// over (field, direction), and an arm that no test takes is an arm that can silently order by
    /// the wrong column — the kind of defect that looks like "the list is a bit odd" rather than
    /// like a failure, and that only shows up against a real database because the ordering is
    /// translated to SQL rather than evaluated in memory.
    /// </summary>
    [DockerTheory]
    [InlineData("Name", "Asc", "Alpha,Beta,Gamma")]
    [InlineData("Name", "Desc", "Gamma,Beta,Alpha")]
    [InlineData("Price", "Asc", "Gamma,Alpha,Beta")]
    [InlineData("Price", "Desc", "Beta,Alpha,Gamma")]
    [InlineData("CreatedAt", "Asc", "Beta,Gamma,Alpha")]
    [InlineData("CreatedAt", "Desc", "Alpha,Gamma,Beta")]
    public async Task Every_sort_column_orders_by_the_column_it_names(
        string sortBy, string direction, string expected)
    {
        await factory.AddProductAsync(new ProductBuilder()
            .Named("Alpha").Priced(20m).CreatedAt(Baseline.AddDays(3)).Build());
        await factory.AddProductAsync(new ProductBuilder()
            .Named("Beta").Priced(30m).CreatedAt(Baseline.AddDays(1)).Build());
        await factory.AddProductAsync(new ProductBuilder()
            .Named("Gamma").Priced(10m).CreatedAt(Baseline.AddDays(2)).Build());

        var page = await factory.CreateClient().GetFromJsonAsync<PagedResponse<ProductDto>>(
            $"/api/v1/products?sortBy={sortBy}&direction={direction}");

        page!.Items.Select(p => p.Name).Should().Equal(expected.Split(','));
    }

    [DockerFact]
    public async Task Search_matches_a_case_insensitive_substring()
    {
        await factory.AddProductAsync(new ProductBuilder().Named("Ergonomic Desk Lamp").Build());
        await factory.AddProductAsync(new ProductBuilder().Named("Office Chair").Build());

        var page = await factory.CreateClient()
            .GetFromJsonAsync<PagedResponse<ProductDto>>("/api/v1/products/search?name=DESK");

        page!.Items.Should().ContainSingle().Which.Name.Should().Be("Ergonomic Desk Lamp");
    }

    [DockerFact]
    public async Task A_percent_sign_in_the_search_term_is_not_a_wildcard()
    {
        await factory.AddProductAsync(new ProductBuilder().Named("100% Cotton Shirt").Build());
        await factory.AddProductAsync(new ProductBuilder().Named("Wool Jumper").Build());

        var page = await factory.CreateClient()
            .GetFromJsonAsync<PagedResponse<ProductDto>>("/api/v1/products/search?name=100%25");

        page!.Items.Should().ContainSingle().Which.Name.Should().Be("100% Cotton Shirt");
    }

    [DockerFact]
    public async Task An_underscore_in_the_search_term_is_not_a_wildcard()
    {
        await factory.AddProductAsync(new ProductBuilder().Named("model_x").Build());
        await factory.AddProductAsync(new ProductBuilder().Named("modelax").Build());

        var page = await factory.CreateClient()
            .GetFromJsonAsync<PagedResponse<ProductDto>>("/api/v1/products/search?name=model_x");

        page!.Items.Should().ContainSingle().Which.Name.Should().Be("model_x");
    }

    [DockerFact]
    public async Task Price_bounds_are_inclusive_and_combine_with_the_name_filter()
    {
        await factory.AddProductAsync(new ProductBuilder().Named("Lamp A").Priced(10m).Build());
        await factory.AddProductAsync(new ProductBuilder().Named("Lamp B").Priced(20m).Build());
        await factory.AddProductAsync(new ProductBuilder().Named("Lamp C").Priced(30m).Build());
        await factory.AddProductAsync(new ProductBuilder().Named("Chair").Priced(20m).Build());

        var page = await factory.CreateClient().GetFromJsonAsync<PagedResponse<ProductDto>>(
            "/api/v1/products/search?name=Lamp&minPrice=10&maxPrice=20");

        page!.Items.Select(p => p.Name).Should().BeEquivalentTo(["Lamp A", "Lamp B"]);
    }

    [DockerFact]
    public async Task An_inverted_price_range_is_a_400_against_minPrice()
    {
        var response = await factory.CreateClient()
            .GetAsync("/api/v1/products/search?minPrice=500&maxPrice=100");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Code.Should().Be("validation_failed");
        problem.Errors.Should().ContainKey("minPrice");
    }

    [DockerFact]
    public async Task A_non_numeric_price_is_a_400_in_the_same_envelope()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/products/search?minPrice=abc");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Code.Should().Be("validation_failed");
    }

    [DockerFact]
    public async Task No_matches_is_a_200_with_an_empty_list()
    {
        await factory.AddProductAsync(new ProductBuilder().Named("Lamp").Build());

        var page = await factory.CreateClient().GetFromJsonAsync<PagedResponse<ProductDto>>(
            "/api/v1/products/search?name=nothing-matches-this");

        page!.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    [DockerFact]
    public async Task Get_by_id_returns_an_ETag_and_honours_If_None_Match()
    {
        var product = await factory.AddProductAsync(new ProductBuilder().Named("Lamp").Build());
        var client = factory.CreateClient();

        var first = await client.GetAsync($"/api/v1/products/{product.Id}");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var etag = first.Headers.ETag!.ToString();
        etag.Should().NotBeNullOrWhiteSpace();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/products/{product.Id}");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);

        var second = await client.SendAsync(request);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
        (await second.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    [DockerTheory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_non_positive_id_is_a_400(int id)
    {
        var response = await factory.CreateClient().GetAsync($"/api/v1/products/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [DockerFact]
    public async Task A_missing_id_is_a_404_and_is_not_cached()
    {
        var client = factory.CreateClient();

        var missing = await client.GetAsync("/api/v1/products/999");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var authed = await factory.AuthenticatedClientAsync("author@example.com");
        var created = await authed.PostAsJsonAsync("/api/v1/products", NewProduct());
        var dto = (await created.Content.ReadFromJsonAsync<ProductDto>())!;

        var found = await client.GetAsync($"/api/v1/products/{dto.Id}");
        found.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [DockerFact]
    public async Task Creating_a_product_requires_a_token()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/products", NewProduct());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Code.Should().Be("missing_token");
    }

    [DockerFact]
    public async Task Create_returns_201_with_a_usable_Location_header()
    {
        var client = await factory.AuthenticatedClientAsync("author@example.com");

        var response = await client.PostAsJsonAsync("/api/v1/products", NewProduct());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var location = response.Headers.Location!.ToString();
        location.Should().StartWith("/api/v1/products/");

        var followed = await factory.CreateClient().GetAsync(location);
        followed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [DockerFact]
    public async Task Create_trims_the_name_and_nulls_a_blank_description()
    {
        var client = await factory.AuthenticatedClientAsync("author@example.com");

        var response = await client.PostAsJsonAsync("/api/v1/products",
            NewProduct(name: "  Desk Lamp  ", description: "   "));

        var dto = (await response.Content.ReadFromJsonAsync<ProductDto>())!;

        dto.Name.Should().Be("Desk Lamp");
        dto.Description.Should().BeNull();
        dto.UpdatedAt.Should().BeNull();
    }

    [DockerFact]
    public async Task Create_never_exposes_the_creating_user()
    {
        var client = await factory.AuthenticatedClientAsync("author@example.com");

        var response = await client.PostAsJsonAsync("/api/v1/products", NewProduct());
        var raw = await response.Content.ReadAsStringAsync();

        raw.Should().NotContain("createdBy").And.NotContain("isDeleted");
    }

    [DockerTheory]
    [InlineData("", 10)]
    [InlineData("   ", 10)]
    [InlineData("ok", -1)]
    [InlineData("ok", 1_000_000)]
    public async Task Create_rejects_invalid_input_with_a_field_scoped_400(string name, decimal price)
    {
        var client = await factory.AuthenticatedClientAsync("author@example.com");

        var response = await client.PostAsJsonAsync("/api/v1/products",
            new CreateProductRequest { Name = name, Price = price });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Code.Should().Be("validation_failed");
        problem.Errors.Should().NotBeEmpty();
    }

    [DockerFact]
    public async Task Duplicate_names_are_allowed()
    {
        var client = await factory.AuthenticatedClientAsync("author@example.com");

        var first = await client.PostAsJsonAsync("/api/v1/products", NewProduct(name: "Lamp"));
        var second = await client.PostAsJsonAsync("/api/v1/products", NewProduct(name: "Lamp"));

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [DockerFact]
    public async Task Update_replaces_every_field_and_preserves_CreatedAt()
    {
        var client = await factory.AuthenticatedClientAsync("author@example.com");

        var created = await client.PostAsJsonAsync("/api/v1/products", NewProduct());
        var dto = (await created.Content.ReadFromJsonAsync<ProductDto>())!;

        var updated = await client.PutAsJsonAsync($"/api/v1/products/{dto.Id}",
            new UpdateProductRequest { Name = "Renamed", Description = null, Price = 99.95m });

        updated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await factory.CreateClient().GetFromJsonAsync<ProductDto>($"/api/v1/products/{dto.Id}");

        after!.Name.Should().Be("Renamed");
        after.Description.Should().BeNull("PUT is a full replacement, not a patch");
        after.Price.Should().Be(99.95m);
        after.CreatedAt.Should().Be(dto.CreatedAt, "CreatedAt is immutable");
        after.UpdatedAt.Should().NotBeNull();
    }

    [DockerFact]
    public async Task Update_with_a_stale_If_Match_is_a_409()
    {
        var client = await factory.AuthenticatedClientAsync("author@example.com");

        var created = await client.PostAsJsonAsync("/api/v1/products", NewProduct());
        var dto = (await created.Content.ReadFromJsonAsync<ProductDto>())!;

        var get = await factory.CreateClient().GetAsync($"/api/v1/products/{dto.Id}");
        var etag = get.Headers.ETag!.ToString();

        await client.PutAsJsonAsync($"/api/v1/products/{dto.Id}",
            new UpdateProductRequest { Name = "First Writer", Price = 1m });

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/products/{dto.Id}")
        {
            Content = JsonContent.Create(new UpdateProductRequest { Name = "Second Writer", Price = 2m })
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Code.Should().Be("concurrency_conflict");

        var after = await factory.CreateClient().GetFromJsonAsync<ProductDto>($"/api/v1/products/{dto.Id}");
        after!.Name.Should().Be("First Writer", "the rejected write must not have been applied");
    }

    [DockerFact]
    public async Task Update_with_a_current_If_Match_succeeds()
    {
        var client = await factory.AuthenticatedClientAsync("author@example.com");

        var created = await client.PostAsJsonAsync("/api/v1/products", NewProduct());
        var dto = (await created.Content.ReadFromJsonAsync<ProductDto>())!;

        var get = await factory.CreateClient().GetAsync($"/api/v1/products/{dto.Id}");
        var etag = get.Headers.ETag!.ToString();

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/products/{dto.Id}")
        {
            Content = JsonContent.Create(new UpdateProductRequest { Name = "Renamed", Price = 5m })
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [DockerFact]
    public async Task Updating_a_missing_product_is_a_404()
    {
        var client = await factory.AuthenticatedClientAsync("author@example.com");

        var response = await client.PutAsJsonAsync("/api/v1/products/999",
            new UpdateProductRequest { Name = "Ghost", Price = 1m });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [DockerFact]
    public async Task A_User_role_token_cannot_delete_and_gets_403_not_401()
    {
        var product = await factory.AddProductAsync(new ProductBuilder().Build());
        var client = await factory.AuthenticatedClientAsync("plain@example.com", UserRole.User);

        var response = await client.DeleteAsync($"/api/v1/products/{product.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Code.Should().Be("insufficient_role");
    }

    [DockerFact]
    public async Task An_Admin_can_delete_and_the_row_survives_for_audit()
    {
        var product = await factory.AddProductAsync(new ProductBuilder().Named("Doomed").Build());
        var admin = await factory.AuthenticatedClientAsync("admin@example.com", UserRole.Admin);

        var response = await admin.DeleteAsync($"/api/v1/products/{product.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await factory.CreateClient().GetAsync($"/api/v1/products/{product.Id}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var second = await admin.DeleteAsync($"/api/v1/products/{product.Id}");
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [DockerFact]
    public async Task Deleting_without_a_token_is_a_401()
    {
        var product = await factory.AddProductAsync(new ProductBuilder().Build());

        var response = await factory.CreateClient().DeleteAsync($"/api/v1/products/{product.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [DockerFact]
    public async Task A_write_invalidates_the_cached_first_page()
    {
        await factory.AddProductAsync(new ProductBuilder().Named("Original").Build());

        var anonymous = factory.CreateClient();

        var before = await anonymous.GetFromJsonAsync<PagedResponse<ProductDto>>("/api/v1/products");
        before!.TotalCount.Should().Be(1);

        var author = await factory.AuthenticatedClientAsync("author@example.com");
        await author.PostAsJsonAsync("/api/v1/products", NewProduct(name: "Brand New"));

        var after = await anonymous.GetFromJsonAsync<PagedResponse<ProductDto>>("/api/v1/products");

        after!.TotalCount.Should().Be(2);
        after.Items.Should().Contain(p => p.Name == "Brand New");
    }

    [DockerFact]
    public async Task An_update_invalidates_the_cached_entity()
    {
        var author = await factory.AuthenticatedClientAsync("author@example.com");

        var created = await author.PostAsJsonAsync("/api/v1/products", NewProduct());
        var dto = (await created.Content.ReadFromJsonAsync<ProductDto>())!;

        var anonymous = factory.CreateClient();
        await anonymous.GetAsync($"/api/v1/products/{dto.Id}");

        await author.PutAsJsonAsync($"/api/v1/products/{dto.Id}",
            new UpdateProductRequest { Name = "Updated Name", Price = 1m });

        var after = await anonymous.GetFromJsonAsync<ProductDto>($"/api/v1/products/{dto.Id}");

        after!.Name.Should().Be("Updated Name");
    }

    [DockerFact]
    public async Task Every_error_response_is_problem_json_with_a_code_and_a_traceId()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/products/999");

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();

        problem!.Code.Should().Be("resource_not_found");
        problem.Status.Should().Be(404);
        problem.TraceId.Should().NotBeNullOrWhiteSpace();
        problem.Instance.Should().Be("/api/v1/products/999");
    }

    [DockerFact]
    public async Task A_supplied_correlation_id_is_echoed_back()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        request.Headers.Add("X-Correlation-Id", "test-correlation-1234");

        var response = await client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle()
            .Which.Should().Be("test-correlation-1234");
    }

    [DockerFact]
    public async Task The_correlation_header_and_the_body_traceId_are_the_same_value()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products/999");
        request.Headers.Add("X-Correlation-Id", "test-correlation-5678");

        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();

        problem!.TraceId.Should().Be("test-correlation-5678");
        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle()
            .Which.Should().Be(problem.TraceId);
    }

    [DockerFact]
    public async Task A_traceId_is_present_even_when_the_caller_supplies_nothing()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/products/999");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();

        problem!.TraceId.Should().NotBeNullOrWhiteSpace();
        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle()
            .Which.Should().Be(problem.TraceId);
    }

    [DockerFact]
    public async Task Health_endpoints_are_anonymous_and_live_does_not_touch_the_database()
    {
        var client = factory.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
