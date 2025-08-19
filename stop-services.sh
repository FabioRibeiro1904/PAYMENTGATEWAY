#!/bin/bash

echo "🛑 Parando todos os serviços do Payment Gateway..."
echo ""

# Função para parar um serviço
stop_service() {
    local service_name=$1
    local pid_file="logs/$service_name.pid"
    
    if [ -f "$pid_file" ]; then
        local pid=$(cat "$pid_file")
        if kill -0 $pid 2>/dev/null; then
            echo "🟡 Parando $service_name (PID: $pid)..."
            kill $pid
            sleep 2
            
            # Forçar parada se necessário
            if kill -0 $pid 2>/dev/null; then
                echo "🔴 Forçando parada do $service_name..."
                kill -9 $pid
            fi
            
            echo "✅ $service_name parado"
        else
            echo "⚠️  $service_name já estava parado"
        fi
        rm -f "$pid_file"
    else
        echo "⚠️  Arquivo PID não encontrado para $service_name"
    fi
}

# Parar serviços
stop_service "Frontend"
stop_service "PaymentGateway.ApiGateway"
stop_service "PaymentGateway.Users"
stop_service "PaymentGateway.Payments"

# Parar PaymentGateway.TransactionProcessor manualmente (background process)
echo "🟡 Parando PaymentGateway.TransactionProcessor..."
pkill -f "PaymentGateway.TransactionProcessor" && echo "✅ PaymentGateway.TransactionProcessor parado" || echo "⚠️  PaymentGateway.TransactionProcessor já estava parado"

# Parar qualquer processo dotnet e vite rodando nas portas específicas
echo ""
echo "🔍 Verificando processos nas portas 5173, 5080, 5076 e 5077..."

for port in 5173 5080 5076 5077; do
    pid=$(lsof -ti:$port)
    if [ ! -z "$pid" ]; then
        echo "🔴 Parando processo na porta $port (PID: $pid)"
        kill -9 $pid
    fi
done

echo ""
echo "✅ Todos os serviços foram parados!"
echo ""
echo "📋 Para verificar se algum processo ainda está rodando:"
echo "lsof -i :5173 -i :5080 -i :5076 -i :5077"
