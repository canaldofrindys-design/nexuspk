import Link from "next/link";
import type { ComponentProps, ReactNode } from "react";

const variants = {
  gold: "bg-gradient-to-b from-gold-bright to-gold text-night border border-gold-bright/60",
  arcane: "bg-gradient-to-b from-arcane to-[#2f7fd1] text-night border border-arcane/60",
  ghost: "bg-night/60 text-gold border border-bronze hover:border-gold",
} as const;

type Props = Omit<ComponentProps<typeof Link>, "className"> & {
  variant?: keyof typeof variants;
  icon?: ReactNode;
  size?: "md" | "lg";
};

export function ButtonLink({ variant = "gold", icon, size = "md", children, ...rest }: Props) {
  const sizing = size === "lg" ? "px-5 py-2.5 text-base" : "px-3.5 py-1.5 text-sm";
  return (
    <Link
      {...rest}
      className={`btn-glow inline-flex items-center gap-2 rounded-sm font-semibold ${sizing} ${variants[variant]}`}
    >
      {icon}
      {children}
    </Link>
  );
}
