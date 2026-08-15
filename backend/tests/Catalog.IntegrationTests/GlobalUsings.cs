global using System.IdentityModel.Tokens.Jwt;
global using System.Net;
global using System.Net.Http.Headers;
global using System.Net.Http.Json;
global using System.Security.Claims;
global using System.Text;
global using System.Text.Json.Serialization;

global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Mvc.Testing;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.IdentityModel.Tokens;

global using Catalog.Application.Common.Interfaces;
global using Catalog.Application.Features.Auth;
global using Catalog.Application.Features.Products;
global using Catalog.Domain.Entities;
global using Catalog.Domain.Enums;
global using Catalog.Domain.ValueObjects;
global using Catalog.Infrastructure.Persistence;
global using Catalog.IntegrationTests.Fixtures;
global using FluentAssertions;
global using Npgsql;
global using Testcontainers.PostgreSql;
