-- Esconder personagens dos rankings do site (home, nível, PvP, SoD, busca e perfil).
-- NÃO apaga nada do jogo: só tira o personagem da vitrine do site. Para voltar a mostrar, apague a linha da lista.
-- Rode no SQL Server Management Studio da VPS, um bloco por vez (selecione o bloco e aperte F5).
-- O site mostra a mudança em até 5 minutos.
--
-- O portal já esconde sozinho:
--   * contas que estão em UserDB.dbo.GameMasters;
--   * personagens com classe "Desconhecida" (código fora de 1 a 11);
--   * contas, personagens e começos de nome desta lista (PortalDB.dbo.RankingExclusion).


-- 1) CONFERIR: quem aparece hoje com classe desconhecida (já fica fora do site com esta versão)
SELECT lt.szID AS Conta, lt.Nick AS Personagem, lt.Class AS CodigoClasse, lt.Level AS Nivel
FROM ServerDB.dbo.LevelTable AS lt
WHERE ISNULL(lt.Class, 0) NOT BETWEEN 1 AND 11
ORDER BY lt.Level DESC;

-- 2) CONFERIR: contas marcadas como GM no jogo (já ficam fora do site)
SELECT Account AS Conta, GameMasterLevel AS NivelGM FROM UserDB.dbo.GameMasters ORDER BY Account;

-- 3) CONFERIR: a lista do site (contas, personagens e começos de nome escondidos)
SELECT Kind AS Tipo, Value AS Valor, Note AS Observacao, CreatedUtc AS IncluidoEmUtc
FROM PortalDB.dbo.RankingExclusion ORDER BY Kind, Value;

-- 4) CONFERIR: os 30 primeiros do ranking como o site vai mostrar, com a conta, para achar algum ADM que faltou
SELECT TOP 30 lt.szID AS Conta, lt.Nick AS Personagem, lt.Class AS CodigoClasse, lt.Level AS Nivel
FROM ServerDB.dbo.LevelTable AS lt
WHERE lt.Level > 0 AND ISNULL(lt.Class, 0) BETWEEN 1 AND 11
  AND NOT EXISTS (SELECT 1 FROM UserDB.dbo.GameMasters AS gm WHERE gm.Account COLLATE DATABASE_DEFAULT = lt.szID COLLATE DATABASE_DEFAULT)
  AND NOT EXISTS (SELECT 1 FROM PortalDB.dbo.RankingExclusion AS x
                  WHERE (x.Kind = 'conta'      AND x.Value COLLATE DATABASE_DEFAULT = lt.szID COLLATE DATABASE_DEFAULT)
                     OR (x.Kind = 'personagem' AND x.Value COLLATE DATABASE_DEFAULT = LTRIM(RTRIM(lt.Nick)) COLLATE DATABASE_DEFAULT)
                     OR (x.Kind = 'prefixo'    AND LEFT(LTRIM(RTRIM(lt.Nick)), LEN(x.Value)) COLLATE DATABASE_DEFAULT = x.Value COLLATE DATABASE_DEFAULT))
ORDER BY lt.Level DESC, lt.Nick;


-- 5) ESCONDER: troque os nomes de exemplo e rode. Rodar duas vezes não duplica.
--    'conta'      = todos os personagens daquela conta (login do jogo)
--    'personagem' = só aquele personagem
--    'prefixo'    = todo personagem cujo nome começa com o texto, ex.: [ADM]
DECLARE @novos TABLE (Kind varchar(12), Value nvarchar(32), Note nvarchar(200));
INSERT INTO @novos (Kind, Value, Note) VALUES
    ('conta',      N'LOGIN_DO_ADM',      N'Conta da equipe'),
    ('personagem', N'NOME_DO_PERSONAGEM', N'Personagem de teste');

INSERT INTO PortalDB.dbo.RankingExclusion (Kind, Value, Note)
SELECT n.Kind, LTRIM(RTRIM(n.Value)), n.Note
FROM @novos AS n
WHERE n.Value NOT IN (N'LOGIN_DO_ADM', N'NOME_DO_PERSONAGEM')
  AND NOT EXISTS (SELECT 1 FROM PortalDB.dbo.RankingExclusion AS x WHERE x.Kind = n.Kind AND x.Value = LTRIM(RTRIM(n.Value)));


-- 6) VOLTAR A MOSTRAR: apaga só a linha da lista (o personagem continua intacto no jogo)
-- DELETE FROM PortalDB.dbo.RankingExclusion WHERE Kind = 'personagem' AND Value = N'NOME_DO_PERSONAGEM';
