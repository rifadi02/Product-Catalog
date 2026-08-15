/** Client-side validation — PDR §4. */
import { describe, expect, it } from 'vitest';
import {
  loginSchema,
  productSchema,
  registerSchema,
  searchFilterSchema,
  toWriteRequest,
} from './schemas';

const firstMessage = (result: { success: boolean; error?: { issues: { message: string }[] } }) =>
  result.error?.issues[0]?.message;

describe('loginSchema', () => {
  it('accepts a seeded account', () => {
    expect(
      loginSchema.safeParse({ email: 'admin@demo.local', password: 'Admin#2026Demo' }).success,
    ).toBe(true);
  });

  it('uses the backend message strings', () => {
    expect(firstMessage(loginSchema.safeParse({ email: '', password: 'x' }))).toBe(
      'Email is required.',
    );
    expect(firstMessage(loginSchema.safeParse({ email: 'a@b.co', password: '' }))).toBe(
      'Password is required.',
    );
  });

  it('does not apply complexity rules on login', () => {
    // The account may predate the current rules — never block a sign-in for a weak-looking
    // password (§4.1).
    expect(loginSchema.safeParse({ email: 'a@b.co', password: 'password' }).success).toBe(true);
  });

  it('treats a whitespace-only email as missing, matching server-side trimming', () => {
    expect(firstMessage(loginSchema.safeParse({ email: '   ', password: 'x' }))).toBe(
      'Email is required.',
    );
  });
});

describe('registerSchema', () => {
  const valid = {
    email: 'newuser@example.com',
    password: 'Str0ngPassw0rd',
    confirmPassword: 'Str0ngPassw0rd',
  };

  it('accepts a password that satisfies every rule', () => {
    expect(registerSchema.safeParse(valid).success).toBe(true);
  });

  it('does not require a symbol', () => {
    // There is no symbol requirement on the backend — adding one would reject valid passwords
    // before they were ever sent (§4.2).
    expect(registerSchema.safeParse({ ...valid, password: 'Abcdefg1', confirmPassword: 'Abcdefg1' }).success).toBe(
      true,
    );
  });

  it('rejects denylisted passwords even when they satisfy complexity', () => {
    // Password123 has upper, lower, digit and 11 characters — and is still refused (§4.2).
    const result = registerSchema.safeParse({
      ...valid,
      password: 'Password123',
      confirmPassword: 'Password123',
    });

    expect(result.success).toBe(false);
    expect(firstMessage(result)).toBe('Password is too common. Choose something less guessable.');
  });

  it('matches the denylist case-insensitively', () => {
    expect(
      registerSchema.safeParse({ ...valid, password: 'PASSWORD123', confirmPassword: 'PASSWORD123' })
        .success,
    ).toBe(false);
  });

  it('reports a mismatch under confirmPassword, like the server does', () => {
    const result = registerSchema.safeParse({ ...valid, confirmPassword: 'Different1' });

    expect(result.success).toBe(false);
    expect(result.error?.issues[0]?.path).toEqual(['confirmPassword']);
    expect(firstMessage(result)).toBe('Passwords do not match.');
  });
});

describe('productSchema', () => {
  it('accepts a price of exactly zero', () => {
    // The seed data includes a Rp 0,00 product; a falsy check anywhere near this field is a bug
    // (§4.3).
    const result = productSchema.safeParse({ name: 'Zero-Cost Kit', description: '', price: '0' });
    expect(result.success).toBe(true);
  });

  it('rejects an empty price rather than coercing it to zero', () => {
    const result = productSchema.safeParse({ name: 'Lamp', description: '', price: '' });
    expect(result.success).toBe(false);
    expect(firstMessage(result)).toBe('Price is required.');
  });

  it('accepts the maximum price and rejects one penny more', () => {
    expect(productSchema.safeParse({ name: 'A', description: '', price: '999999.99' }).success).toBe(
      true,
    );

    const result = productSchema.safeParse({ name: 'A', description: '', price: '1000000' });
    expect(firstMessage(result)).toBe('Price must be between 0 and 999999.99.');
  });

  it('rejects a name of only whitespace', () => {
    const result = productSchema.safeParse({ name: '   ', description: '', price: '1' });
    expect(firstMessage(result)).toBe('Name is required.');
  });

  it('accepts the 2000-character description and refuses 2001', () => {
    expect(
      productSchema.safeParse({ name: 'A', description: 'x'.repeat(2000), price: '1' }).success,
    ).toBe(true);
    expect(
      productSchema.safeParse({ name: 'A', description: 'x'.repeat(2001), price: '1' }).success,
    ).toBe(false);
  });
});

describe('toWriteRequest', () => {
  it('sends null for a blank description, never an empty string', () => {
    expect(toWriteRequest({ name: 'Lamp', description: '   ', price: '10' })).toEqual({
      name: 'Lamp',
      description: null,
      price: 10,
    });
  });

  it('trims like the server does and rounds the price to 2dp', () => {
    expect(toWriteRequest({ name: '  Desk Lamp  ', description: '  Warm  ', price: '2.355' })).toEqual(
      { name: 'Desk Lamp', description: 'Warm', price: 2.36 },
    );
  });

  it('preserves a genuine zero price', () => {
    expect(toWriteRequest({ name: 'Kit', description: '', price: '0' }).price).toBe(0);
  });
});

describe('searchFilterSchema', () => {
  it('accepts empty filters', () => {
    expect(searchFilterSchema.safeParse({ name: '', minPrice: '', maxPrice: '' }).success).toBe(
      true,
    );
  });

  it('reports the cross-field failure under minPrice, matching the server key', () => {
    const result = searchFilterSchema.safeParse({ name: '', minPrice: '50', maxPrice: '10' });

    expect(result.success).toBe(false);
    expect(result.error?.issues[0]?.path).toEqual(['minPrice']);
    expect(firstMessage(result)).toBe('minPrice must be less than or equal to maxPrice.');
  });

  it('accepts equal bounds — the range is inclusive', () => {
    expect(
      searchFilterSchema.safeParse({ name: '', minPrice: '10', maxPrice: '10' }).success,
    ).toBe(true);
  });

  it('accepts one bound without the other', () => {
    expect(searchFilterSchema.safeParse({ name: '', minPrice: '10', maxPrice: '' }).success).toBe(
      true,
    );
  });
});
