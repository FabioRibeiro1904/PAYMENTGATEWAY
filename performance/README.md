# 🚀 Performance Testing Guide

## 📊 Overview
Este diretório contém todos os scripts e configurações para testes de performance do PaymentGateway.

## 🛠️ Tools Used
- **k6**: Load testing framework
- **Artillery**: Alternative load testing tool
- **Grafana**: Performance metrics visualization
- **Prometheus**: Metrics collection

## 📋 Test Types

### 1. 🔄 Load Testing
Testa a aplicação sob carga normal esperada.
```bash
k6 run load-test.js
```

### 2. ⚡ Spike Testing
Testa a resposta a picos súbitos de tráfego.
```bash
k6 run --config spike-test.json load-test.js
```

### 3. 💪 Stress Testing
Testa os limites da aplicação.
```bash
k6 run --config stress-test.json load-test.js
```

### 4. ⏱️ Endurance Testing
Testa a estabilidade por longos períodos.
```bash
k6 run --config endurance-test.json load-test.js
```

## 🎯 Performance Targets

### Response Time Targets
- **P95 < 2 seconds**: 95% das requisições devem ser processadas em menos de 2s
- **P99 < 5 seconds**: 99% das requisições devem ser processadas em menos de 5s
- **Mean < 1 second**: Tempo médio de resposta deve ser menor que 1s

### Throughput Targets
- **Minimum RPS**: 100 requests per second
- **Target RPS**: 500 requests per second
- **Peak RPS**: 1000 requests per second

### Error Rate Targets
- **Error Rate < 0.1%**: Taxa de erro deve ser menor que 0.1%
- **Availability > 99.9%**: Sistema deve estar disponível 99.9% do tempo

### Resource Utilization
- **CPU < 70%**: Utilização de CPU deve ficar abaixo de 70%
- **Memory < 80%**: Utilização de memória deve ficar abaixo de 80%
- **Disk I/O < 80%**: Utilização de disco deve ficar abaixo de 80%

## 📊 Metrics Collected

### Application Metrics
- Response time (P50, P95, P99)
- Request rate (RPS)
- Error rate
- Active users
- Database query time
- Cache hit rate

### Infrastructure Metrics
- CPU utilization
- Memory usage
- Disk I/O
- Network I/O
- Database connections
- Redis connections

### Business Metrics
- Payment processing rate
- Transaction success rate
- User registration rate
- Authentication success rate

## 🔧 Test Configuration

### Environment Variables
```bash
export BASE_URL="http://localhost:5080"
export TEST_DURATION="5m"
export VIRTUAL_USERS="50"
export RPS_TARGET="100"
```

### Test Data
- **Users**: 1000 test users pre-created
- **Payments**: Random payment amounts between R$ 1-1000
- **Currencies**: BRL, USD, EUR
- **Recipients**: Pool of 100 test recipients

## 📈 Monitoring Integration

### Grafana Dashboards
- **Application Performance**: Response times, throughput, errors
- **Infrastructure**: CPU, memory, disk, network
- **Business KPIs**: Payment metrics, user activity

### Alerts Configuration
- High response time (> 5s)
- High error rate (> 1%)
- High CPU usage (> 80%)
- Database connection pool exhaustion

## 🚀 Running Tests

### Prerequisites
```bash
# Install k6
brew install k6

# Install Artillery (alternative)
npm install -g artillery

# Start monitoring stack
docker-compose -f monitoring/docker-compose.yml up -d
```

### Basic Load Test
```bash
k6 run \
  --vus 50 \
  --duration 5m \
  --out influxdb=http://localhost:8086/k6 \
  load-test.js
```

### Advanced Test with Custom Config
```bash
k6 run \
  --config test-config.json \
  --env BASE_URL=http://staging.payment.com \
  --env TEST_TYPE=load \
  load-test.js
```

### CI/CD Integration
```yaml
# GitHub Actions example
- name: Run Performance Tests
  run: |
    k6 run \
      --out json=results.json \
      --quiet \
      performance/load-test.js
    
    # Parse results and fail if thresholds not met
    node scripts/parse-performance-results.js results.json
```

## 📊 Results Analysis

### Key Performance Indicators (KPIs)
1. **Latency**: P95 response time < 2s
2. **Throughput**: > 100 RPS sustained
3. **Reliability**: < 0.1% error rate
4. **Scalability**: Linear performance up to 1000 concurrent users

### Performance Regression Detection
- Automated comparison with baseline metrics
- Alert on performance degradation > 10%
- Trend analysis for gradual performance decline

### Optimization Recommendations
- Database query optimization
- Caching strategy implementation
- Connection pool tuning
- CDN configuration for static assets

## 🔄 Continuous Performance Testing

### Scheduled Tests
- **Hourly**: Quick smoke tests (1 minute, 10 users)
- **Daily**: Load tests (10 minutes, 100 users)
- **Weekly**: Stress tests (30 minutes, 500 users)
- **Monthly**: Endurance tests (2 hours, 200 users)

### Performance Budgets
- Response time budget: 2s
- Bundle size budget: 500KB
- Database query budget: 100ms
- Memory usage budget: 512MB per service

## 📋 Troubleshooting

### Common Issues
1. **High Response Times**
   - Check database connection pool
   - Verify cache hit rates
   - Monitor garbage collection

2. **High Error Rates**
   - Review application logs
   - Check external service dependencies
   - Verify load balancer configuration

3. **Memory Leaks**
   - Monitor heap usage over time
   - Check for unclosed connections
   - Review garbage collection metrics

### Performance Tuning Checklist
- [ ] Database indexes optimized
- [ ] Connection pooling configured
- [ ] Caching strategy implemented
- [ ] Static assets compressed
- [ ] CDN configured
- [ ] Load balancer tuned
- [ ] Auto-scaling policies set
- [ ] Resource limits configured
