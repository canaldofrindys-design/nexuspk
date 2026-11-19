import Link from "next/link";
import { getCurrentPlayer } from "@/features/auth/session";

const goldButton =
  "btn-glow hidden rounded-sm nx-btn px-3 py-1.5 text-sm font-semibold sm:block";

export function GuestLinks() {
  return (
    <>
      <Link href="/login" className={goldButton}>
        Entrar
      </Link>
    </>
  );
}

/** Mostra Entrar (o cadastro já está no menu) ou o link para o painel do jogador. */
export async function HeaderAccount() {
  const player = await getCurrentPlayer();
  if (!player) return <GuestLinks />;
  return (
    <Link href="/painel" className={goldButton}>
      Painel
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
