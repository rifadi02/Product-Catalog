import { useForm, type UseFormReturn } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { productSchema, type ProductFormValues } from '../../lib/schemas';
import type { Product } from '../../api/types';

export type ProductFormApi = UseFormReturn<ProductFormValues>;

/**
 * Create defaults: everything empty — `price` blank rather than `0`, so a genuine Rp 0,00 product
 * is a deliberate entry rather than an accident (§3.6.5).
 */
export const EMPTY_PRODUCT_FORM: ProductFormValues = { name: '', description: '', price: '' };

/** `description: null` maps to `''` in the form, and back to `null` on submit if blank (§3.6.5). */
export function toFormValues(product: Product): ProductFormValues {
  return {
    name: product.name,
    description: product.description ?? '',
    price: product.price.toFixed(2),
  };
}

export function useProductForm(defaultValues: ProductFormValues): ProductFormApi {
  return useForm<ProductFormValues>({
    resolver: zodResolver(productSchema),
    mode: 'onBlur',
    defaultValues,
  });
}
