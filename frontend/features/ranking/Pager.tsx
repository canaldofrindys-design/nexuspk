import Link from "next/link";

export function Pager({ page, pageSize, total, href }: { page: number; pageSize: number; total: number; href: (page: number) => string }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  if (pages <= 1) return null;
  return (
    <nav aria-label="Páginas" className="mt-3 flex items-center justify-between text-sm">
      {page > 1 ? (
        <Link href={href(page - 1)} className="text-arcane hover:underline">
          Página anterior
        </Link>
      ) : (
        <span />
      )}
      <span className="tabular text-silver/70">
        {page} de {pages}
      </span>
      {page < pages ? (
        <Link href={href(page + 1)} className="text-arcane hover:underline">
          Próxima página
        </Link>
      ) : (
        <span />
      )}
    </nav>
  );
}
