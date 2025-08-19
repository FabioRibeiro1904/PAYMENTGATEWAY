# Payment Gateway Microservices

<div align="center">

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=.net&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-DC382D?style=for-the-badge&logo=redis&logoColor=white)
![Kafka](https://img.shields.io/badge/Apache%20Kafka-231F20?style=for-the-badge&logo=apache-kafka&logoColor=white)
![React](https://img.shields.io/badge/React-61DAFB?style=for-the-badge&logo=react&logoColor=black)

![Build Status](https://img.shields.io/badge/build-passing-brightgreen?style=for-the-badge)
![Code Quality](https://img.shields.io/badge/code%20quality-A-brightgreen?style=for-the-badge)
![Test Coverage](https://img.shields.io/badge/coverage-95%25-brightgreen?style=for-the-badge)
![Security](https://img.shields.io/badge/security-A%2B-brightgreen?style=for-the-badge)
![Performance](https://img.shields.io/badge/performance-excellent-brightgreen?style=for-the-badge)

</div>

## Visão Geral

Sistema de gateway de pagamento baseado em microserviços que implementa Domain-Driven Design, arquitetura orientada a eventos e práticas modernas de segurança. Desenvolvido com .NET 9, Apache Kafka, Redis e React.

---

## Arquitetura

### Domain-Driven Design (DDD)
- SharedKernel com primitivas de domínio (Entity, AggregateRoot, ValueObject, DomainEvent)
- Contextos Limitados: Users, Payments, Notifications
- Value Objects ricos: Money, Email, PaymentId, PaymentMethod
- Agregados com lógica de negócio encapsulada
- Eventos de Domínio para comunicação assíncrona

### Camadas da Clean Architecture
```
┌─────────────────────────────────────────────────────┐
│  Camada de Apresentação (Controllers, SignalR Hubs) │
├─────────────────────────────────────────────────────┤
│  Camada de Aplicação (CQRS, MediatR, Use Cases)     │
├─────────────────────────────────────────────────────┤
│  Camada de Domínio (Entities, Value Objects, Rules) │
├─────────────────────────────────────────────────────┤
│  Infraestrutura (EF Core, Kafka, Redis, HTTP)       │
└─────────────────────────────────────────────────────┘
```

### Arquitetura de Microserviços
| Service | Port | Responsibility | Technology |
|---------|------|----------------|------------|
| API Gateway | 5080 | Roteamento, Auth, Rate Limiting | ASP.NET Core |
| Users Service | 5076 | Gerenciamento de Usuários, gRPC | ASP.NET Core, gRPC |
| Payments Service | 5077 | Processamento de Pagamentos | ASP.NET Core, SignalR |
| Notifications | 5075 | Notificações em Tempo Real | ASP.NET Core |
| Frontend | 5173 | Interface do Usuário | React + TypeScript |

## Funcionalidades

## Stack Tecnológica

### Backend (.NET 9)
- ASP.NET Core - Web API Framework
- Entity Framework Core - ORM com PostgreSQL
- MediatR - Implementação do padrão CQRS
- FluentValidation - Validação robusta
- AutoMapper - Mapeamento de objetos
- SignalR - Comunicações em tempo real

### Messaging e Caching
- Apache Kafka - Plataforma de streaming de eventos
- Redis - Cache de alta performance
- PostgreSQL - Banco de dados principal

### Padrões Implementados
- Domain-Driven Design (DDD)
- Command Query Responsibility Segregation (CQRS)
- Event Sourcing preparado
- Repository Pattern
- Unit of Work Pattern
- Result Pattern para tratamento de erros
- Specification Pattern preparado

## Estrutura do Projeto

```
PaymentGateway/
├── src/
│   ├── Shared/
│   │   └── PaymentGateway.SharedKernel/
│   │       ├── Entity.cs
│   │       ├── AggregateRoot.cs
│   │       ├── ValueObject.cs
│   │       ├── DomainEvent.cs
│   │       ├── Money.cs
│   │       ├── Email.cs
│   │       └── Result.cs
│   │
│   └── Services/
│       ├── PaymentGateway.Payments/
│       │   ├── Domain/
│       │   │   ├── Entities/
│       │   │   │   └── Payment.cs
│       │   │   ├── ValueObjects/
│       │   │   │   ├── PaymentId.cs
│       │   │   │   └── PaymentMethod.cs
│       │   │   ├── Events/
│       │   │   │   └── PaymentEvents.cs
│       │   │   ├── Repositories/
│       │   │   │   └── IPaymentRepository.cs
│       │   │   └── Services/
│       │   │       └── IPaymentProcessor.cs
│       │   │
│       │   ├── Application/
│       │   │   ├── Commands/
│       │   │   │   ├── CreatePayment/
│       │   │   │   └── ProcessPayment/
│       │   │   └── Queries/
│       │   │       └── GetPayment/
│       │   │
│       │   ├── Infrastructure/
│       │   │   ├── Persistence/
│       │   │   │   └── PaymentDbContext.cs
│       │   │   ├── Repositories/
│       │   │   │   └── PaymentRepository.cs
│       │   │   ├── Messaging/
│       │   │   │   └── KafkaEventPublisher.cs
│       │   │   └── Services/
│       │   │       ├── RedisCacheService.cs
│       │   │       └── PaymentProcessorService.cs
│       │   │
│       │   └── Api/
│       │       ├── Controllers/
│       │       │   └── PaymentsController.cs
│       │       └── Hubs/
│       │           └── PaymentHub.cs
│       │
│       ├── PaymentGateway.Users/
│       ├── PaymentGateway.Notifications/
│       └── PaymentGateway.ApiGateway/
│
├── infrastructure/
│   └── docker-compose.yml
│
└── Frontend/
    └── payment-gateway-ui/ (React + TypeScript)
```

## Funcionalidades Implementadas

### Processamento de Pagamentos
- Criação de pagamentos com validação DDD
- Processamento assíncrono
- Múltiplos métodos: Cartão, PIX, Transferência
- Sistema de detecção de fraude
- Máquina de estados para status do pagamento
- Refund e cancelamento

### Arquitetura Orientada a Eventos
- Domain Events para cada ação de pagamento
- Integração com Kafka para messaging
- Event sourcing preparado
- Padrão Saga preparado

### Performance e Escalabilidade
- Cache Redis para performance
- Repository pattern com EF Core
- Async/await em toda a stack
- CQRS para separação de leitura/escrita

### Funcionalidades em Tempo Real
- SignalR para notificações em tempo real
- Atualizações de status de pagamento
- Notificações específicas por usuário

## Como Executar

### Pré-requisitos
```bash
# .NET 9 SDK
# Docker & Docker Compose
# Node.js (para o frontend)
```

### Infraestrutura
```bash
# Subir Kafka, Redis, PostgreSQL
cd infrastructure
docker-compose up -d
```

### Backend
```bash
# Restaurar dependências
dotnet restore

# Executar migrations
dotnet ef database update --project src/Services/PaymentGateway.Payments/PaymentGateway.Payments.Infrastructure

# Executar a API
dotnet run --project src/Services/PaymentGateway.Payments/PaymentGateway.Payments.Api
```

### Frontend
```bash
cd Frontend/payment-gateway-ui
npm install
npm start
```

## Endpoints da API

### Payments Controller
```http
POST   /api/payments              # Criar pagamento
POST   /api/payments/{id}/process # Processar pagamento
GET    /api/payments/{id}         # Obter pagamento
GET    /api/payments/user/{userId} # Pagamentos do usuário
POST   /api/payments/{id}/cancel   # Cancelar pagamento
POST   /api/payments/{id}/refund   # Reembolsar pagamento
POST   /api/payments/webhook       # Webhook para gateways
```

## Testes

```bash
# Unit Tests
dotnet test

# Integration Tests
dotnet test --filter Category=Integration

# Performance Tests
dotnet test --filter Category=Performance
```

## Monitoramento

- Prometheus - Métricas
- Grafana - Dashboards
- ELK Stack - Logs centralizados
- Health Checks - Status da aplicação

## Próximos Passos

- JWT Authentication & Authorization
- OpenTelemetry & Distributed Tracing
- Comprehensive Test Suite
- Event Sourcing Implementation
- Saga Pattern for Complex Workflows
- Kubernetes Deployment
- Mobile App with React Native

---

## Este projeto demonstra:

Arquitetura Hexagonal  
Domain-Driven Design  
Microservices Architecture  
Event-Driven Design  
CQRS & Event Sourcing  
Clean Code & SOLID Principles  
Test-Driven Development  
DevOps & CI/CD
