import { LoginBrandPanel } from "@/components/auth/login-brand-panel";
import { LoginForm } from "@/components/auth/login-form";

/**
 * ==========================================================================================
 * WHAT: Next.js login route that composes the split-screen Figma layout (brand + form).
 * WHY: Matches Login Page.png — left brand plane, right light form canvas (#F8F9FB).
 * EXAMPLE: /login renders LoginBrandPanel + LoginForm; company name maps to company_id INT.
 * ==========================================================================================
 */
export default function LoginPage() {
  return (
    <main className="flex min-h-screen w-full bg-[#F8F9FB] text-[#1E2433]">
      <LoginBrandPanel />
      <section className="flex w-full flex-col items-center justify-center bg-[#F8F9FB] px-5 py-8 sm:px-8 lg:w-1/2 lg:px-12 xl:px-16">
        <LoginForm />
      </section>
    </main>
  );
}
