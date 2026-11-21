-- 0010 — "Drops raros da semana" da home: a equipe anuncia os drops no painel (Banners e destaques).
-- Guardado em dbo.SiteSetting com a chave 'drops' (JSON validado pela API). O banco do jogo não é alterado.
ALTER TABLE dbo.SiteSetting DROP CONSTRAINT CK_SiteSetting_Key;
ALTER TABLE dbo.SiteSetting ADD CONSTRAINT CK_SiteSetting_Key CHECK ([Key] IN ('server','downloads','drops'));
