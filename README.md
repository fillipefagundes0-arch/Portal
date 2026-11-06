# Portal de Automação

Primeira versão em ASP.NET Core 10 + Blazor Server, preparada para Windows/IIS e SQL Server. Sem integração com PLCs ou alarmes em tempo real.

## Funcionalidades

- Consulta por TAG, descrição, área e alarme (até 200 registros por busca).
- Cadastro de instrumentos com TAG única, especificações, localização e rastreio da última alteração.
- Múltiplos alarmes por instrumento: código único no instrumento, descrição, prioridade, limite, unidade, causas, efeitos e ação recomendada.
- Upload de fotos/documentos até 20 MB, nomes internos aleatórios e download autorizado. Arquivos ficam fora da pasta pública; download como anexo.
- Identidade Windows completa `DOMINIO\chave`, sem guardar ou receber senha no portal.
- Visualização: consulta/download. Planejamento: também cadastro e anexos. Automação: também alarmes. Administração de acessos é uma permissão adicional independente do perfil.
- Registro de alterações e prevenção de sobrescrita concorrente do instrumento.

## Desenvolvimento local

Requer SDK .NET 10. Em Linux use SQLite somente em Development. Cada tarefa em nuvem já está isolada: use este checkout; não crie worktrees sem solicitação.

```bash
cd /workspace/Portal
mkdir -p Portal/App_Data
ASPNETCORE_ENVIRONMENT=Development DatabaseProvider=Sqlite \
ConnectionStrings__Portal='Data Source=App_Data/portal.db' \
LocalDevelopmentIdentity=true BootstrapAdmin='LOCAL\dev' \
dotnet run --project Portal --urls http://127.0.0.1:5080
```

A identidade local só funciona para conexões loopback em Development; não use esse modo em produção. Sem ela, acesso sem identidade Windows é negado. Não há dados fictícios adicionados automaticamente. O primeiro administrador é criado apenas se a tabela de usuários estiver vazia. A identidade recebida do IIS deve corresponder à chave completa cadastrada, incluindo domínio; não pressupomos o nome do domínio da Petrobras.

```bash
dotnet build Portal/Portal.csproj
dotnet run --project tests/Portal.Tests.csproj
```

## Windows e instalador

O pacote inicial é ZIP com instalador PowerShell, executado como administrador. Não é ainda um MSI/EXE assinado. A execução real no Windows/AD precisa de homologação pela TI.

Pré-requisitos:
1. Windows com IIS, Windows Authentication, WebSockets e ferramentas de gerenciamento habilitados.
2. ASP.NET Core **10 Hosting Bundle** oficial (módulo IIS necessário mesmo com publicação self-contained).
3. SQL Server Express ou SQL Server corporativo com banco e permissões preparados pela TI; certificado SQL confiável. Não desabilite a validação TLS para contornar erros de certificado.
4. Servidor/estações e navegador com políticas corporativas de autenticação integrada apropriadas. A TI deve verificar domínio, zona da intranet, Kerberos/SPN e, se necessário, identidade gMSA do pool.

Produza o pacote em uma máquina com SDK e acesso ao NuGet:

```powershell
.\installer\Build-Package.ps1
# Extraia artifacts\PortalAutomacao.zip, abra PowerShell como administrador:
.\Install-Portal.ps1 -AdminKey 'DOMINIO\chave' -ConnectionString 'Server=SERVIDOR\SQLEXPRESS;Database=PortalAutomacao;Integrated Security=True;Encrypt=True;TrustServerCertificate=False'
```

O instalador copia a aplicação, configura o site/pool IIS, desabilita acesso anônimo, habilita Windows Authentication e prepara permissões de arquivos. Não instala SQL, não ingressa no domínio, não solicita senhas e não configura DNS/certificados/firewall. O pool usa ApplicationPoolIdentity; a conexão integrada ao SQL é feita **pelo pool**, não pelo usuário conectado. A TI deve conceder acesso ao banco à identidade apropriada do pool (local ou corporativa). Não habilite impersonação/delegação para resolver isso sem projeto da TI.

Configure HTTPS no IIS com certificado corporativo, hostname e binding antes da liberação. A porta HTTP inicial é para homologação local controlada. Valide com contas reais: administrador inicial, cada perfil e conta não cadastrada. Uma resposta em `/health` só confirma processo; a consulta e gravação precisam funcionar para confirmar o banco e o acesso. `BootstrapAdmin` não promove usuários em banco já existente e pode ser removido da configuração após bootstrap.

## Dados, backup e atualizações

- Banco contém cadastros, permissões, metadados de anexos e auditoria. Os arquivos ficam em `C:\ProgramData\PortalAutomacao\attachments`.
- Faça backup consistente do SQL **e** dessa pasta; teste restauração e restrinja acesso aos backups. A auditoria inicial registra operações, não um histórico completo de valores anteriores.
- Esta versão usa `EnsureCreated` para banco novo. Não possui migrações de esquema; alterações futuras exigem migração planejada antes da atualização. Não execute atualização de esquema automaticamente sobre dados existentes.
- O instalador recusa sobrescrever configuração existente. Para atualizar, faça backup, pare apenas o site/pool PortalAutomacao, publique para uma pasta temporária, preserve `appsettings.Production.json` e a pasta externa de dados, aplique eventual migração revisada, substitua binários e reinicie. Mantenha o pacote anterior para rollback compatível com o banco.
- Homologar limites/quota de armazenamento, antivírus corporativo nos anexos, retenção da auditoria, HTTPS e recuperação de desastre antes de produção. Validação de extensão/tamanho não substitui antivírus.

## Limites da entrega inicial

Sem exclusão de instrumentos/anexos, integração com SAP/PLC, notificações, importação em lote ou painel de alarmes ativos. SQL Server, IIS, instalador e AD precisam ser testados em Windows corporativo; testes locais usam SQLite. Não há credenciais ou conexão real à Petrobras incluídas.
