-- 0009 — quem fica fora dos rankings públicos do site (home, nível, PvP, SoD, busca e perfil).
-- Só esconde no site: nenhum personagem é apagado ou alterado no banco do jogo.
-- Além desta lista, o portal já esconde as contas de UserDB.dbo.GameMasters e os personagens com classe
-- desconhecida (código fora de 1 a 11).
--
-- Kind:
--   'conta'      todos os personagens desta conta do jogo (login);
--   'personagem' só este personagem (nome exato, sem diferenciar maiúsculas);
--   'prefixo'    todo personagem cujo nome começa com este texto, ex.: [ADM].
-- Para incluir ou tirar nomes depois, use database/scripts/esconder-do-ranking.sql.
CREATE TABLE dbo.RankingExclusion (
    Id          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RankingExclusion PRIMARY KEY,
    Kind        varchar(12)    NOT NULL,
    Value       nvarchar(32)   NOT NULL,
    Note        nvarchar(200)  NULL,
    CreatedUtc  datetime2(0)   NOT NULL CONSTRAINT DF_RankingExclusion_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_RankingExclusion_Kind CHECK (Kind IN ('conta', 'personagem', 'prefixo')),
    CONSTRAINT UQ_RankingExclusion UNIQUE (Kind, Value)
);

-- Contas da equipe que aparecem com AdminLv 4 nos logs de comandos de GM do servidor (LogAdmin).
INSERT INTO dbo.RankingExclusion (Kind, Value, Note) VALUES
    ('conta', N'drakken',  N'Conta da equipe (LogAdmin)'),
    ('conta', N'drakken1', N'Conta da equipe (LogAdmin)'),
    ('conta', N'drakken2', N'Conta da equipe (LogAdmin)'),
    ('conta', N'drakken3', N'Conta da equipe (LogAdmin)'),
    ('conta', N'drakken4', N'Conta da equipe (LogAdmin)'),
    ('conta', N'ADMPK',    N'Conta da equipe (LogAdmin)'),
    ('conta', N'ADMPK1',   N'Conta da equipe (LogAdmin)');

-- Personagens da equipe pelo nome.
INSERT INTO dbo.RankingExclusion (Kind, Value, Note) VALUES
    ('prefixo', N'[ADM]',   N'Personagens da equipe'),
    ('prefixo', N'[GM]',    N'Personagens da equipe'),
    ('prefixo', N'[STAFF]', N'Personagens da equipe'),
    ('personagem', N'ADMNEXUS',    N'Personagem da equipe'),
    ('personagem', N'ADMPK',       N'Personagem da equipe'),
    ('personagem', N'ADMPKzao',    N'Personagem da equipe'),
    ('personagem', N'VINICIUSADM', N'Personagem da equipe'),
    ('personagem', N'TesteADM',    N'Personagem de teste');
