// Ponto de entrada do Next no IIS. O módulo ASP.NET Core do IIS inicia este processo,
// informa a porta em ASPNETCORE_PORT e repassa as requisições para ela.
// O servidor do Next escuta só em 127.0.0.1: de fora, só o IIS alcança.
process.env.PORT = process.env.ASPNETCORE_PORT || process.env.PORT || "3000";
process.env.HOSTNAME = "127.0.0.1";
require("./server.js");
