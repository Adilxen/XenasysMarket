import { redirect } from "next/navigation";

/** Legacy /products/new → drawer create on listing (Vendor gold standard). */
export default function NewProductPage() {
  redirect("/products");
}
