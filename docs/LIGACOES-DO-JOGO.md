# Ligações do jogo e do launcher com o site

O jogo e o launcher não falam só com o banco: eles chamam páginas do site antigo. Para o domínio principal poder passar para o portal sem quebrar nada, o portal atende os mesmos endereços com os **mesmos arquivos** do site antigo.

## O que o jogo e o launcher chamam

Levantado nos arquivos do site antigo (`C:\wwwroot`). Onde está "não confirmado", a pasta é ligada do mesmo jeito, mas não achei quem chama.

| Endereço | Quem usa | O que faz | Banco |
|---|---|---|---|
| `ServerMain/isClanMember.asp` | jogo | dados do clã do personagem (nome, líder, vice, ícone, nota, flags) | lê `ClanDB.CL`, `UL`; grava `UL.MIconCnt` |
| `ServerMain/isCheckClanName.asp`, `isCheckClanJang.asp`, `isCheckClanNum.asp`, `isCheckClanwon.asp`, `isPFlag.asp`, `isKflag.asp` | jogo | consultas do clã | lê `CL`, `UL` |
| `ServerMain/GetAllMyClanMember.asp` | jogo | lista de membros | lê `CL`, `UL` |
| `ServerMain/clanInsertClanWon.asp` | jogo | convidar membro (máximo 30) | grava `CL.MemCnt`, `UL` |
| `ServerMain/clanWonRelease.asp`, `clanWonSelfLeave.asp` | jogo | expulsar e sair do clã | grava `CL.MemCnt`, apaga de `UL` |
| `ServerMain/clanChipChange.asp` | jogo | trocar o líder | grava `CL.ClanZang`, `CL.UserID` |
| `ServerMain/ClanSubChipUpdate.asp`, `ClanSubChipRelease.asp` | jogo | nomear e tirar o vice (`UL.Permi = 2`) | grava `UL` |
| `ServerMain/ClanRemove.asp` | jogo | desfazer o clã | apaga de `CL` e `UL` |
| `ServerMain/sod2info.asp` | jogo | informações de Bellatra (SoD) por clã | lê `CL`, `UL` |
| `Clan/verificarcl.asp` | jogo | criar o clã (próximo número de ícone em `LI`) | grava `LI`, `CL`, `UL` |
| `ClanContent/<número>.bmp` | jogo | ícone de cada clã (`CL.MIconCnt`) | — |
| `updater/version.txt`, `Updates.xml`, `<versão>.zip`, `patch.json` | launcher | atualização do cliente | — |
| `include/status.php` | não confirmado | imagem online/offline pelo teste da porta 32009, que na VPS é do SQL Server e não do jogo (o jogo escuta na 35002) | — |
| `att/`, `rankAPI/` | não confirmado | nível e rankings em PHP | lê o banco do jogo |

As respostas do clã são texto no formato `Code=<n>` seguido de CR (`\r`), um campo por linha.

`ServerMain/ClanRemove.asp` e `Clan/verificarcl.asp` só agem se o `userid` estiver na tabela `CT` do `ClanDB` (o personagem está no jogo). Chamados de um navegador, respondem que a ação tem de ser feita pelo jogo.

## Como o portal atende esses endereços

`deploy/windows/Connect-LegacyPaths.ps1` (o `Install-Portal.ps1` já chama, se existir `C:\wwwroot`):

- cria no site `NexusPortal` do IIS uma **aplicação** para cada pasta acima que existe no site antigo, apontando para a mesma pasta física;
- as aplicações rodam no **pool do site antigo**, com o mesmo usuário anônimo e a mesma configuração de ASP (página de código, idioma, caminhos pai). Os arquivos de configuração deles (`settings.asp` e outros) continuam onde estão; o portal não lê nem copia nenhum;
- o `web.config` do portal usa `inheritInChildApplications="false"`: essas pastas não passam pelo Node, não recebem o redirecionamento para https (o jogo chama por http) nem os cabeçalhos do portal;
- o PHP dessas pastas usa o mesmo `php-cgi.exe` registrado no IIS para o site antigo.

O site antigo não é alterado e continua respondendo pelos mesmos endereços. Durante uma atualização do portal (`Deploy-Portal.ps1`), essas pastas continuam no ar: elas não dependem do Node nem da API.

```powershell
C:\NexusPortal\current\deploy\Connect-LegacyPaths.ps1                     # liga (padrão C:\wwwroot)
C:\NexusPortal\current\deploy\Connect-LegacyPaths.ps1 -LegacyRoot D:\site # outra pasta
C:\NexusPortal\current\deploy\Connect-LegacyPaths.ps1 -Remove             # desliga
```

O teste no Windows do GitHub monta um site antigo de laboratório com uma página ASP no formato das do clã, liga ao portal e confere a resposta exata (`Code=0\r`), o ícone, o `version.txt`, que a página inicial continua sendo o portal e que tudo segue igual depois de atualizar e voltar versão.

## Para a troca do domínio

Quem chama esses endereços é o jogo (cliente ou servidor) e o launcher, pelo nome configurado neles. Se chamarem pelo **domínio**, passam a cair no portal na troca e continuam funcionando por causa das aplicações acima. Se chamarem pelo **IP**, continuam caindo no site antigo, que segue no ar.

## Riscos que já existem no site antigo

Não foram criados pelo portal, mas passam a valer também pelo endereço do portal:

- **Login `sa`.** As páginas do clã conectam ao SQL Server com o login `sa` e a senha escrita em `ServerMain/SODsettings.asp` (e provavelmente em `settings.asp`). Qualquer falha nessas páginas dá controle total do SQL Server. Recomendado: trocar a senha do `sa` e criar um login só com `SELECT`, `INSERT`, `UPDATE` e `DELETE` em `ClanDB.dbo.CL`, `UL`, `LI` e `SELECT` em `CT`, e usá-lo nos `settings`.
- **SQL montado com texto.** As páginas juntam os parâmetros direto na consulta. A maioria passa por um filtro de palavras (`AntiSQL`), mas `ClanRemove.asp` e `Clan/verificarcl.asp` não, e o filtro não cobre todos os casos.
- **Sem autenticação.** O `ticket` que o jogo manda não é conferido. Quem souber os endereços pode chamar as páginas que não exigem o personagem em `CT` (expulsar membro, trocar líder, vice).

O caminho para resolver isso sem mudar o jogo é reescrever essas páginas na API do portal com consultas parametrizadas e um login de permissão mínima, mantendo os mesmos endereços e respostas. Fica para quando você decidir.
