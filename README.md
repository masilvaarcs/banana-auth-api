# 🔐 banana-auth-api

Serviço de autenticação do sistema de reserva de salas da Banana Ltda.

## 📋 Responsabilidade na Arquitetura

Este serviço é responsável exclusivamente por cadastro e autenticação de usuários, emitindo tokens JWT assinados que serão validados pelo serviço de reservas (banana-reservas-api). Ele não consome o backend Python diretamente.

```text
[Frontend] -> POST /login -> [banana-auth-api] -> retorna JWT
[Frontend] -> requisições de reserva + JWT -> [banana-reservas-api] -> valida JWT localmente
```

## 📦 Estrutura de Repositórios

Este repositório é independente e representa apenas o Projeto 1 (banana-auth-api).

- Não existe solução (.sln) agregando os 3 projetos.
- O serviço é executado isoladamente via BananaAuth.Api.csproj.
- A integração com os demais acontece por contrato HTTP + JWT compartilhado por ambiente.

## 🛠️ Stack Tecnológica

| Tecnologia | Versão | Justificativa |
| --- | --- | --- |
| .NET | 8 (LTS) | Versão com suporte de longa duração, maior estabilidade e performance |
| ASP.NET Core Web API | 8.x | Framework robusto e maduro para APIs RESTful em C# |
| Entity Framework Core | 8.x | ORM oficial da Microsoft, obrigatório conforme especificação |
| SQL Server | Local | Banco relacional já disponível no ambiente de desenvolvimento |
| BCrypt.Net-Next | 4.0.3 | Algoritmo seguro e amplamente adotado para hash de senhas |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.0 | Biblioteca oficial para validação JWT em .NET |
| Swashbuckle (Swagger) | 6.6.2 | Documentação interativa automática da API |

## ✅ Pré-requisitos

- .NET 8 SDK
- SQL Server (local)
- Visual Studio 2022 ou VS Code + C# Dev Kit

## ⚙️ Variáveis de Ambiente

Crie o arquivo appsettings.Development.json na raiz do projeto (não commitado):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=BananaAuth;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Secret": "CHAVE_SECRETA_COMPARTILHADA_MINIMO_32_CHARS",
    "Issuer": "banana-auth-service",
    "Audience": "banana-app",
    "ExpirationMinutes": 60,
    "RefreshExpirationDays": 7
  },
  "AllowedOrigins": [
    "http://localhost:3000",
    "http://localhost:5173"
  ]
}
```

Importante:
- O valor de `Jwt:Secret` deve ser idêntico ao `JWT_SECRET` configurado no banana-reservas-api.
- `AllowedOrigins` controla quais origens do frontend podem chamar esta API (CORS). Adicione a URL do frontend em produção quando necessário.

## 🚀 Como Rodar Localmente

```bash
# 1. Entrar na pasta do projeto
cd banana-auth-api

# 2. Restaurar dependências
dotnet restore BananaAuth.Api.csproj

# 3. Criar o banco de dados e aplicar migrations
dotnet ef database update --project BananaAuth.Api.csproj

# 4. Rodar o projeto
dotnet run --project BananaAuth.Api.csproj
```

A API estará disponível em:

- <https://localhost:7045>
- <http://localhost:5156>
- Swagger: <https://localhost:7045/swagger>

## 📡 Endpoints

| Método | Rota | Descrição | Auth |
| --- | --- | --- | --- |
| POST | /api/auth/register | Cadastro de novo usuário | ❌ |
| POST | /api/auth/login | Login e emissão de JWT | ❌ |
| POST | /api/auth/refresh | Renovação de token (bônus) | ❌ |
| GET | /api/auth/health | Health check do serviço | ❌ |

### POST /api/auth/register

Requisição:
```json
{
  "name": "Marcos Silva",
  "email": "marcos@email.com",
  "password": "senha123"
}
```

Resposta (201 Created):
```json
{
  "userId": "uuid-do-usuario",
  "name": "Marcos Silva",
  "email": "marcos@email.com"
}
```

### POST /api/auth/login

```json
{
  "email": "marcos@email.com",
  "password": "senha123"
}
```

Resposta do login:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "...",
  "expiresIn": 3600
}
```

## 🔑 Estrutura do JWT Emitido

```json
{
  "sub": "uuid-do-usuario",
  "email": "marcos@email.com",
  "name": "Marcos Silva",
  "iss": "banana-auth-service",
  "aud": "banana-app",
  "iat": 1234567890,
  "exp": 1234571490
}
```

## 🔗 Integração com o banana-reservas-api

A chave de assinatura JWT (Jwt:Secret) é compartilhada entre os dois serviços exclusivamente por configuração de ambiente. O backend Python valida o token localmente sem realizar chamada HTTP ao serviço C#.

## 📁 Estrutura do Projeto

```text
banana-auth-api/
├── Controllers/
│   └── AuthController.cs
├── DTOs/
│   ├── RegisterRequestDto.cs
│   ├── LoginRequestDto.cs
│   ├── RefreshRequestDto.cs
│   ├── RegisterResponseDto.cs
│   └── AuthResponseDto.cs
├── Models/
│   ├── User.cs
│   └── RefreshToken.cs
├── Data/
│   └── AppDbContext.cs
├── Services/
│   ├── IAuthService.cs
│   ├── AuthService.cs
│   ├── ITokenService.cs
│   └── TokenService.cs
├── Configuration/
│   └── JwtSettings.cs
├── Common/
│   └── ApiException.cs
├── Migrations/
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
├── BananaAuth.Api.csproj
└── README.md
```
