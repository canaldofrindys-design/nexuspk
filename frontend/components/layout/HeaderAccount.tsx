import Link from "next/link";
import { getCurrentPlayer } from "@/features/auth/session";

const goldButton =
  "btn-glow hidden rounded-sm border border-gold-bright/60 bg-gradient-to-b from-gold-bright to-gold px-3 py-1.5 text-sm font-semibold text-night sm:block";

export function GuestLinks() {
  return (
    <>
      <Link href="/login" className="hidden px-2 text-sm text-silver hover:text-gold sm:block">
        Entrar
      </Link>
      <Link href="/criar-conta" className={goldButton}>
        Criar conta
      </Link>
    </>
  );
}

/** Mostra Entrar/Criar conta ou o login do jogador com link para o painel. */
export async function HeaderAccount() {
  const player = await getCurrentPlayer();
  if (!player) return <GuestLinks />;
  return (
    <Link href="/painel" className={goldButton}>
      Painel de {player.login}
    </Link>
  );
}

const mobileLink = "px-2 py-2 text-silver hover:text-gold";

export function MobileGuestLinks() {
  return (
    <>
      <Link href="/login" className={mobileLink}>
        Entrar
      </Link>
      <Link href="/criar-conta" className={`${mobileLink} text-gold-bright`}>
        Criar conta
      </Link>
    </>
  );
}

export async function MobileAccount() {
  const player = await getCurrentPlayer();
  if (!player) return <MobileGuestLinks />;
  return (
    <Link href="/painel" className={`${mobileLink} text-gold-bright`}>
      Painel de {player.login}
    </Link>
  );
}
