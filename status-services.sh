#!/bin/bash

echo "📊 STATUS DOS SERVIÇOS PAYMENT GATEWAY"
echo "======================================"
echo ""

# Função para verificar um serviço
check_service() {
    local service_name=$1
    local url=$2
    local port=$3
    
    echo -n "🔍 $service_name ($url): "
    
    if curl -s --max-time 3 "$url/health" > /dev/null 2>&1; then
        echo "✅ ONLINE"
    else
        echo "❌ OFFLINE"
        
        # Verificar se há processo na porta
        local pid=$(lsof -ti:$port 2>/dev/null)
        if [ ! -z "$pid" ]; then
            echo "   ⚠️  Processo detectado na porta $port (PID: $pid), mas não responde"
        fi
    fi
}

# Verificar infraestrutura
echo "🔧 INFRAESTRUTURA:"
echo -n "├── Redis: "
if docker exec redis redis-cli ping > /dev/null 2>&1; then
    echo "✅ ONLINE"
else
    echo "❌ OFFLINE"
fi

echo -n "├── Kafka: "
if docker exec kafka kafka-broker-api-versions --bootstrap-server localhost:9092 > /dev/null 2>&1; then
    echo "✅ ONLINE"
else
    echo "❌ OFFLINE"
fi

echo -n "└── Zookeeper: "
if docker exec zookeeper bash -c "echo ruok | nc localhost 2181" | grep -q imok 2>/dev/null; then
    echo "✅ ONLINE"
else
    echo "❌ OFFLINE"
fi

echo ""
echo "🎯 SERVIÇOS .NET:"

# Verificar serviços
check_service "PaymentGateway.ApiGateway" "http://localhost:5080" 5080
check_service "PaymentGateway.Users" "http://localhost:5076" 5076
check_service "PaymentGateway.Payments" "http://localhost:5077" 5077

echo ""
echo "🌐 INTERFACES WEB:"
echo "├── API Gateway: http://localhost:5080"
echo "├── Kafka UI: http://localhost:8082"
echo "└── Redis Commander: http://localhost:8081"

echo ""
echo "📁 LOGS:"
if [ -f "logs/PaymentGateway.ApiGateway.log" ]; then
    echo "├── logs/PaymentGateway.ApiGateway.log ($(wc -l < logs/PaymentGateway.ApiGateway.log) linhas)"
else
    echo "├── logs/PaymentGateway.ApiGateway.log (não encontrado)"
fi

if [ -f "logs/PaymentGateway.Users.log" ]; then
    echo "├── logs/PaymentGateway.Users.log ($(wc -l < logs/PaymentGateway.Users.log) linhas)"
else
    echo "├── logs/PaymentGateway.Users.log (não encontrado)"
fi

if [ -f "logs/PaymentGateway.Payments.log" ]; then
    echo "└── logs/PaymentGateway.Payments.log ($(wc -l < logs/PaymentGateway.Payments.log) linhas)"
else
    echo "└── logs/PaymentGateway.Payments.log (não encontrado)"
fi

echo ""
echo "🚀 COMANDOS ÚTEIS:"
echo "├── Iniciar: ./start-services.sh"
echo "├── Parar: ./stop-services.sh"
echo "└── Status: ./status-services.sh"
