#!/bin/bash

echo "🚀 Iniciando Payment Gateway Microservices..."

# Verificar se Docker está rodando
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker não está rodando. Por favor, inicie o Docker."
    exit 1
fi

# Subir infraestrutura
echo "📦 Subindo infraestrutura (Kafka, Redis, PostgreSQL, Monitoring)..."
docker-compose up -d

# Aguardar serviços ficarem prontos
echo "⏳ Aguardando serviços ficarem prontos..."
sleep 30

# Verificar status
echo "✅ Verificando status dos serviços..."
docker-compose ps

echo ""
echo "🎉 Infraestrutura pronta!"
echo "🔗 Acessos:"
echo "   📊 Grafana: http://localhost:3001 (admin/admin)"
echo "   📈 Prometheus: http://localhost:9090"
echo "   🗄️  PostgreSQL: localhost:5432 (postgres/postgres)"
echo "   🚀 Kafka: localhost:9092"
echo "   💾 Redis: localhost:6379"
echo ""
echo "📝 Próximos passos:"
echo "   1. Compile a solution: dotnet build"
echo "   2. Execute os microserviços individualmente"
echo "   3. Teste com Swagger UI"
echo ""
