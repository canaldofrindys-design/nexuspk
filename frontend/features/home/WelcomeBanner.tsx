import Image from "next/image";
import Link from "next/link";
import { kitArt } from "@/components/ui/ArtLink";

/**
 * Banner "Bem-vindo ao NexusPK" do kit visual. Aparece no lugar do carrossel enquanto a equipe
 * não cadastrar banners no painel. As setas e os pontos do recorte original foram removidos,
 * porque aqui não há outros slides.
 */
export function WelcomeBanner() {
  const art = kitArt.banners.destaque;
  return (
    <Link href="/servidor" className="art-link game-window block overflow-hidden p-0!" title="Conheça o servidor">
      <Image
        src={art.src}
        alt="Bem-vindo ao NexusPK: o clássico Priston Tale em uma nova experiência. Conheça o servidor."
        width={art.width}
        height={art.height}
        sizes="(min-width: 1024px) 640px, 100vw"
        unoptimized
        className="h-auto w-full"
      />
    </Link>
  );
}
