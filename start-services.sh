#!/bin/bash

echo "🚀 Iniciando todos os serviços do Payment Gateway..."
echo ""

# Verificar se o Docker está rodando
echo "🔍 Verificando se o Docker está rodando..."
if ! docker ps > /dev/null 2>&1; then
    echo "❌ Docker não está rodando! Execute o Docker Desktop primeiro."
    exit 1
fi

# Verificar se a infraestrutura está rodando
echo "🔍 Verificando infraestrutura..."
if ! docker exec redis redis-cli ping > /dev/null 2>&1; then
    echo "❌ Redis não está rodando! Execute 'docker-compose -f docker-compose.infra.yml up -d' primeiro."
    exit 1
fi

if ! docker exec kafka kafka-broker-api-versions --bootstrap-server localhost:9092 > /dev/null 2>&1; then
    echo "❌ Kafka não está rodando! Execute 'docker-compose -f docker-compose.infra.yml up -d' primeiro."
    exit 1
fi

echo "✅ Infraestrutura OK!"
echo ""

# Função para verificar se uma porta está em uso
check_port() {
    if lsof -Pi :$1 -sTCP:LISTEN -t >/dev/null ; then
        echo "⚠️  Porta $1 já está em uso!"
        return 1
    fi
    return 0
}

# Verificar portas
echo "🔍 Verificando portas..."
check_port 5173 || exit 1
check_port 5075 || exit 1
check_port 5076 || exit 1
check_port 5077 || exit 1
check_port 5080 || exit 1
echo "✅ Portas disponíveis!"
echo ""

# Criar diretório para logs
mkdir -p logs

# Função para iniciar um serviço
start_service() {
    local service_name=$1
    local service_path=$2
    local port=$3
    local base_dir=$(pwd)
    
    echo "🟡 Iniciando $service_name na porta $port..."
    
    cd "$service_path"
    nohup dotnet run --urls "http://localhost:$port" > "$base_dir/logs/$service_name.log" 2>&1 &
    local pid=$!
    
    # Aguardar alguns segundos para o serviço inicializar
    sleep 3
    
    # Verificar se o processo ainda está rodando
    if kill -0 $pid 2>/dev/null; then
        echo "✅ $service_name iniciado com sucesso! (PID: $pid)"
        echo "$pid" > "$base_dir/logs/$service_name.pid"
    else
        echo "❌ Falha ao iniciar $service_name"
        cat "$base_dir/logs/$service_name.log"
        exit 1
    fi
    
    cd "$base_dir"
}

echo "🚀 Iniciando serviços..."
echo ""

# Iniciar PaymentGateway.Users
start_service "PaymentGateway.Users" "src/Services/PaymentGateway.Users" 5076

# Iniciar PaymentGateway.Payments  
start_service "PaymentGateway.Payments" "src/Services/PaymentGateway.Payments" 5077

# Iniciar PaymentGateway.TransactionProcessor (Background Kafka Consumer)
echo "🟡 Iniciando PaymentGateway.TransactionProcessor (Kafka Consumer)..."
base_dir=$(pwd)
cd "src/Services/PaymentGateway.TransactionProcessor"
nohup dotnet run > "$base_dir/logs/PaymentGateway.TransactionProcessor.log" 2>&1 &
PROCESSOR_PID=$!
echo "✅ PaymentGateway.TransactionProcessor iniciado com sucesso! (PID: $PROCESSOR_PID)"
cd "$base_dir"

# Iniciar PaymentGateway.ApiGateway
start_service "PaymentGateway.ApiGateway" "src/Services/PaymentGateway.ApiGateway" 5080

# Iniciar Frontend
echo "🟡 Iniciando Frontend React (Vite)..."
base_dir=$(pwd)
cd "src/Frontend/payment-gateway-frontend"

# Verificar se node_modules existe, se não, instalar dependências
if [ ! -d "node_modules" ]; then
    echo "📦 Instalando dependências do frontend..."
    npm install
fi

nohup npm run dev > "$base_dir/logs/Frontend.log" 2>&1 &
FRONTEND_PID=$!

# Aguardar alguns segundos para o frontend inicializar
sleep 5

# Verificar se o processo ainda está rodando
if kill -0 $FRONTEND_PID 2>/dev/null; then
    echo "✅ Frontend iniciado com sucesso! (PID: $FRONTEND_PID)"
    echo "$FRONTEND_PID" > "$base_dir/logs/Frontend.pid"
else
    echo "❌ Falha ao iniciar Frontend"
    cat "$base_dir/logs/Frontend.log"
fi

cd "$base_dir"

echo ""
echo "🎉 Todos os serviços foram iniciados com sucesso!"
echo ""
echo "📋 STATUS DOS SERVIÇOS:"
echo "├── Frontend React: http://localhost:5173"
echo "├── PaymentGateway.ApiGateway: http://localhost:5080"
echo "├── PaymentGateway.Users: http://localhost:5076"
echo "├── PaymentGateway.Payments: http://localhost:5077"
echo "├── PaymentGateway.TransactionProcessor: Background Kafka Consumer"
echo "├── Kafka UI: http://localhost:8082"
echo "└── Redis Commander: http://localhost:8081"
echo ""

# Testar os serviços
echo "🧪 Testando serviços..."
sleep 2

if curl -s http://localhost:5173 > /dev/null; then
    echo "✅ Frontend React respondendo"
else
    echo "❌ Frontend React não está respondendo"
fi

if curl -s http://localhost:5080/health > /dev/null; then
    echo "✅ PaymentGateway.ApiGateway respondendo"
else
    echo "❌ PaymentGateway.ApiGateway não está respondendo"
fi

if curl -s http://localhost:5076/health > /dev/null; then
    echo "✅ PaymentGateway.Users respondendo"
else
    echo "❌ PaymentGateway.Users não está respondendo"
fi

if curl -s http://localhost:5077/health > /dev/null; then
    echo "✅ PaymentGateway.Payments respondendo"
else
    echo "❌ PaymentGateway.Payments não está respondendo"
fi

echo ""
echo "📁 Logs disponíveis em:"
echo "├── logs/Frontend.log"
echo "├── logs/PaymentGateway.Users.log"
echo "├── logs/PaymentGateway.Payments.log"
echo "└── logs/PaymentGateway.TransactionProcessor.log"
echo ""
echo "🛑 Para parar todos os serviços, execute: ./stop-services.sh"
