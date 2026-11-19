# Kit visual NexusPK

Artes do pacote `NexusPK_Portal_Assets.zip`, convertidas para WebP em `frontend/public/kit/`.
Componentes: `components/ui/KitIcon.tsx` (ícones e medalhas) e `components/ui/ArtLink.tsx` (cards, banners e botões com texto embutido, sempre com texto alternativo e proporção preservada).

| Peça | Onde aparece |
| --- | --- |
| Paleta e CSS (`06_guia/estilo_base.css`) | Cores do tema (`globals.css`), janelas (`.game-window` = `.nx-game-panel`), botões (`.nx-btn` = `.nx-game-button`), menu `.nx-navbar` |
| Prancha `home.png` | Ordem da home: banner + 3 botões, 6 cards, Ranking top geral, patentes, Bosses mundiais, Classes do jogo, Guerra de clãs, Status do servidor |
| Ícones (20) | Menu do cabeçalho e do celular, atalhos do celular, títulos de páginas e janelas, abas do ranking e tipos de evento |
| `inicio-castelo` | O Servidor (menu e título) |
| `noticias` | Notícias, Quadro de avisos |
| `eventos` | Eventos, Agenda, Próximos eventos, aba "Especial", período "Mês" |
| `trofeu` | Ranking (menu e título), atalho do celular, evento "Comemorativo" |
| `ranking-geral-coroa`, `ranking-diario`, `ranking-semanal` | Janela de ranking da home, abas Nível/Geral/Hoje/Semana |
| `pvp` | Aba PvP, janela PvP do personagem, evento PvP |
| `boss-mundial` | Acontecendo agora, evento Boss |
| `guilda`, `guildas-bandeira` | Nome de cada clã, aba Clãs, Comunidade |
| `recompensas-bau` | Encerrados recentemente, evento Drop |
| `mapa-missoes` | Começar a jogar, evento Invasão |
| `loja-moedas` | Taxas do servidor |
| `download`, `guia-do-jogo`, `classes`, `personagem-armadura`, `conta-login`, `jogadores-online` | Download, Suporte, Classes, Personagens/Painel, Conta, Status/Servidor |
| Cards 06–11 | Faixa de destaques logo abaixo da abertura da home (PvP, Bosses, Eventos, Ranking geral/diário/semanal) |
| Banners 01–02 | Topo de Eventos (filtram Especial e Boss) |
| Banners 03–05 | Topo do Ranking (PvP, semanal, diário) |
| Banner 12 (destaque) | Home, no lugar do carrossel enquanto não houver banners no painel (setas e pontos removidos do recorte) |
| Banner 13 (cabeçalho) | Topo de todas as páginas, com o menu em moldura dourada logo abaixo |
| Botões 06–08 | Ao lado do banner da home (Download, Cadastre-se, Começar a jogar) |
| Recortes da prancha (Bosses mundiais grande, Guerra de clãs) | Home, ao lado de Classes do jogo |
| Emblemas (7) | Faixa de patentes da home; ouro, prata e bronze também no pódio dos rankings |
| Botões 01, 02, 05 | O Servidor (Começar a jogar) e Suporte (Abrir chamado) |
| Medalhas ouro, prata, bronze | 1º, 2º e 3º lugar nos rankings |

Ainda sem lugar, porque a página não existe: botões "Doar / Cash" e "Guia do jogo", e o item "Loja" do menu da prancha (no lugar dele entram Notícias e Classes). "Top Boss" da prancha vira "Top Mensal" até existir ranking de boss. Os arquivos estão em `public/kit` prontos para uso.

O logo do pacote é o mesmo de `public/brand/logo-nexuspk.webp`, que foi mantido.

## Cenários pintados e movimento (fase 18)

Sete paisagens em `frontend/public/art/cenas/`, em WebP com três larguras (640, 1024 e 1448/1600 px, de 60 a 350 KB). O registro fica em `components/scene/scenes.ts`.

| Cena | Onde aparece |
| --- | --- |
| `vale-dourado` (vale ao entardecer) | 1ª cena do banner da home, fundo da prancha da home, Ranking, Personagens |
| `luar` (trilha de cristais sob a lua) | 2ª cena do banner, capítulo "Eventos, bosses e PvP", Eventos, Classes |
| `jornada` (aventureiro na estrada) | 3ª cena do banner, capítulo "Primeiros passos", Download, Criar conta |
| `mercado-entardecer` (feira ao pôr do sol) | capítulo "Notícias e comunidade", Notícias |
| `vila-conversa` (moradores na praça) | bloco "Acontecendo no NexusPK" da home, Suporte, Meus chamados |
| `vila-riacho` (vila com riacho e ponte) | O Servidor, Painel do jogador, Entrar e Recuperar conta |
| `torre-vigia` (torre à beira do penhasco) | capítulo "Status, conta e suporte", Status do servidor |

Peças de movimento:

- **Banner animado** (`features/home/HeroBanner.tsx`): três cenas com esmaecer cruzado, aproximação lenta, brilho de sol ou lua, névoa e partículas; troca a cada 8 s e pausa com mouse, foco, aba oculta ou banner fora da tela. Setas, marcadores e as teclas ← → trocam a cena.
- **Fundo que troca ao rolar** (`components/scene/SceneBackdrop.tsx`): qualquer bloco com `data-scene="<cena>"` muda o fundo do site para aquela cena, com esmaecer de 1,6 s, véu noturno e parallax leve. Cada cena só é baixada quando o trecho dela chega perto.
- **Abertura das páginas** (`components/scene/PageHero.tsx`): cenário em parallax com névoa e título em ouro.
- **Capítulos da home** (`features/home/HomeChapters.tsx`): gravura emoldurada com parallax próprio, faixa de luz, numeral romano que flutua com a rolagem e cartões que entram em sequência.

Com "reduzir movimento" no sistema não há troca automática, partículas, parallax nem entradas: o conteúdo aparece parado. No celular vai sempre o arquivo de 1024 px, o fundo não "respira" e o banner usa menos partículas e nenhum raio de luz.
