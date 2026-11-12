import { ViewTransition } from "react";

/**
 * Transição entre páginas: o template é recriado a cada navegação, então a página que sai
 * desfoca e desce, e a que chega sobe com um brilho. O cabeçalho fica parado (ver globals.css).
 * Sem suporte a View Transitions no navegador, a troca é instantânea como antes.
 */
export default function Template({ children }: { children: React.ReactNode }) {
  return (
    <ViewTransition enter="page-enter" exit="page-exit" update="none" default="none">
      <div className="page-shell">{children}</div>
    </ViewTransition>
  );
}
