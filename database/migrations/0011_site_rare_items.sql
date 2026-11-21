-- 0011 — lista de itens raros da home: todo "pegou" desses códigos no log de itens do jogo (ItemLogDB.dbo.IL)
-- aparece sozinho em "Drops raros da semana". Guardado em dbo.SiteSetting com a chave 'rareitems' (JSON validado pela API).
ALTER TABLE dbo.SiteSetting DROP CONSTRAINT CK_SiteSetting_Key;
ALTER TABLE dbo.SiteSetting ADD CONSTRAINT CK_SiteSetting_Key CHECK ([Key] IN ('server','downloads','drops','rareitems'));
