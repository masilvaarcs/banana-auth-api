# 🔐 banana-auth-api

Serviço de autenticação do sistema de reserva de salas da **Banana Ltda.**

## 📋 Responsabilidade na Arquitetura

Este serviço é responsável exclusivamente por **cadastro e autenticação de usuários**, emitindo tokens JWT assinados que serão validados pelo serviço de reservas (`banana-reservas-api`). Ele **não consome** o backend Python diretamente.

```
[Frontend] → POST /login → [banana-auth-api] → retorna JWT
[Frontend] → requisições de reserva + JWT → [banana-reservas-api] → valida JWT localmente
```

## 🛠️ Stack Tecnológica

| Tecnologia | Versão | Justificativa |
|---|---|---|
| .NET | 8 (LTS) | Versão com suporte de longa duração, maior estabilidade e performance |
| ASP.NET Core Web API | 8.x | Framework robusto e maduro para APIs RESTful em C# |
| Entity Framework Core | 8.x | ORM oficial da Microsoft, obrigatório conforme especificação |
| SQL Server | Local | Banco relacional já disponível no ambiente de desenvolvimento |
| BCrypt.Net-Next | latest | Algoritmo seguro e amplamente adotado para hash de senhas |
| System.IdentityModel.Tokens.Jwt | latest | Biblioteca oficial para geração e validação de JWT em .NET |
| Swashbuckle (Swagger) | latest | Documentação interativa automática da API |

## ✅ Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (local)
- Visual Studio 2022 ou VS Code + C# Dev Kit

## ⚙️ Variáveis de Ambiente

Crie o arquivo `appsettings.Development.json` na raiz do projeto (não commitado):

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
  }
}
```

> ⚠️ **Importante:** O valor de `Jwt:Secret` deve ser **idêntico** ao `JWT_SECRET` configurado no `banana-reservas-api`.

## 🚀 Como Rodar Localmente

```bash
# 1. Clonar o repositório
git clone https://github.com/seu-usuario/banana-auth-api.git
cd banana-auth-api

# 2. Restaurar dependências
dotnet restore

# 3. Criar o banco de dados e aplicar migrations
dotnet ef database update

# 4. Rodar o projeto
dotnet run

# A API estará disponível em:
# https://localhost:7001
# http://localhost:5001
# Swagger: https://localhost:7001/swagger
```

## 📦 Pacotes NuGet

```bash
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package BCrypt.Net-Next
dotnet add package Swashbuckle.AspNetCore
```

## 📡 Endpoints

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| POST | `/api/auth/register` | Cadastro de novo usuário | ❌ |
| POST | `/api/auth/login` | Login e emissão de JWT | ❌ |
| POST | `/api/auth/refresh` | Renovação de token *(bônus)* | ❌ |
| GET | `/api/auth/health` | Health check do serviço | ❌ |

### Exemplos de Payload

**POST /api/auth/register**
```json
{
  "name": "Marcos Silva",
  "email": "marcos@email.com",
  "password": "senha123"
}
```

**POST /api/auth/login**
```json
{
  "email": "marcos@email.com",
  "password": "senha123"
}
```

**Resposta do login:**
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

A chave de assinatura JWT (`Jwt:Secret`) é compartilhada entre os dois serviços **exclusivamente via variável de ambiente**. O backend Python valida o token localmente sem realizar nenhuma chamada HTTP a este serviço.

## 📁 Estrutura do Projeto

```
BananaAuth/
├── Controllers/
│   └── AuthController.cs
├── DTOs/
│   ├── RegisterRequestDto.cs
│   ├── LoginRequestDto.cs
│   └── AuthResponseDto.cs
├── Models/
│   └── User.cs
├── Data/
│   └── AppDbContext.cs
├── Services/
│   ├── IAuthService.cs
│   ├── AuthService.cs
│   ├── ITokenService.cs
│   └── TokenService.cs
├── Migrations/
├── appsettings.json
├── appsettings.Development.json  ← não commitado
├── Program.cs
└── README.md
```
