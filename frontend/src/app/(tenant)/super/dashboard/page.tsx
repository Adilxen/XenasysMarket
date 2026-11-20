import { redirect } from "next/navigation";

/** Legacy /super/dashboard → single Dashboard. */
export default function Page() {
  redirect("/dashboard");
}
