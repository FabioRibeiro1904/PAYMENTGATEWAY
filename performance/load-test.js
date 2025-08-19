import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// 📊 Custom metrics
export let errorRate = new Rate('errors');
export let responseTime = new Trend('response_time');
export let requests = new Counter('requests');

// 🎯 Test configuration
export let options = {
  stages: [
    // Warm-up
    { duration: '2m', target: 10 },
    // Ramp-up
    { duration: '5m', target: 50 },
    // Stay at 50 users
    { duration: '10m', target: 50 },
    // Ramp-up to 100 users
    { duration: '5m', target: 100 },
    // Stay at 100 users
    { duration: '10m', target: 100 },
    // Ramp-down
    { duration: '5m', target: 0 },
  ],
  thresholds: {
    // 99% of requests must complete within 5s
    http_req_duration: ['p(99)<5000'],
    // 95% of requests must complete within 2s
    'http_req_duration{status:200}': ['p(95)<2000'],
    // Error rate must be less than 1%
    errors: ['rate<0.01'],
    // Request rate should be above 10 RPS
    http_reqs: ['rate>10'],
  },
};

// 🌐 Base URL configuration
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5080';

// 👤 Test data
const TEST_USERS = [
  { email: 'test1@payment.com', password: 'Test123!@#' },
  { email: 'test2@payment.com', password: 'Test123!@#' },
  { email: 'test3@payment.com', password: 'Test123!@#' },
];

// 🔑 Authentication helper
function authenticate(user) {
  const loginResponse = http.post(`${BASE_URL}/api/auth/login`, {
    email: user.email,
    password: user.password,
  }, {
    headers: { 'Content-Type': 'application/json' },
  });

  check(loginResponse, {
    'login successful': (r) => r.status === 200,
    'token received': (r) => r.json('token') !== '',
  });

  return loginResponse.json('token');
}

// 🧪 Main test scenario
export default function () {
  const user = TEST_USERS[Math.floor(Math.random() * TEST_USERS.length)];
  
  // 🔐 Authentication
  const token = authenticate(user);
  const headers = {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json',
  };

  // 📊 Test user endpoints
  testUserEndpoints(headers);
  
  // 💳 Test payment endpoints
  testPaymentEndpoints(headers);
  
  // 🔔 Test notification endpoints
  testNotificationEndpoints(headers);

  sleep(1);
}

function testUserEndpoints(headers) {
  const userResponse = http.get(`${BASE_URL}/api/users/profile`, { headers });
  
  check(userResponse, {
    'user profile retrieved': (r) => r.status === 200,
    'user data valid': (r) => r.json('id') !== null,
  });

  errorRate.add(userResponse.status !== 200);
  responseTime.add(userResponse.timings.duration);
  requests.add(1);
}

function testPaymentEndpoints(headers) {
  // Test payment creation
  const paymentData = {
    amount: Math.floor(Math.random() * 1000) + 1,
    currency: 'BRL',
    description: 'Performance test payment',
    recipientId: 'test-recipient-id',
  };

  const paymentResponse = http.post(
    `${BASE_URL}/api/payments`,
    JSON.stringify(paymentData),
    { headers }
  );

  check(paymentResponse, {
    'payment created': (r) => r.status === 200 || r.status === 201,
    'payment id returned': (r) => r.json('id') !== null,
  });

  if (paymentResponse.status === 200 || paymentResponse.status === 201) {
    const paymentId = paymentResponse.json('id');
    
    // Test payment status check
    const statusResponse = http.get(
      `${BASE_URL}/api/payments/${paymentId}`,
      { headers }
    );

    check(statusResponse, {
      'payment status retrieved': (r) => r.status === 200,
      'payment status valid': (r) => ['pending', 'processing', 'completed', 'failed'].includes(r.json('status')),
    });
  }

  errorRate.add(paymentResponse.status < 200 || paymentResponse.status >= 400);
  responseTime.add(paymentResponse.timings.duration);
  requests.add(1);
}

function testNotificationEndpoints(headers) {
  const notificationResponse = http.get(`${BASE_URL}/api/notifications`, { headers });
  
  check(notificationResponse, {
    'notifications retrieved': (r) => r.status === 200,
    'notifications array': (r) => Array.isArray(r.json()),
  });

  errorRate.add(notificationResponse.status !== 200);
  responseTime.add(notificationResponse.timings.duration);
  requests.add(1);
}

// 🎯 Spike test scenario
export function spikeTest() {
  const user = TEST_USERS[0];
  const token = authenticate(user);
  
  // Simulate sudden traffic spike
  for (let i = 0; i < 10; i++) {
    const response = http.get(`${BASE_URL}/api/health`, {
      headers: { 'Authorization': `Bearer ${token}` },
    });
    
    check(response, {
      'health check during spike': (r) => r.status === 200,
    });
  }
}

// 🔄 Stress test scenario
export function stressTest() {
  const user = TEST_USERS[0];
  const token = authenticate(user);
  
  // Test system under stress
  const responses = [];
  
  for (let i = 0; i < 100; i++) {
    responses.push(
      http.get(`${BASE_URL}/api/users/profile`, {
        headers: { 'Authorization': `Bearer ${token}` },
      })
    );
  }
  
  const successRate = responses.filter(r => r.status === 200).length / responses.length;
  
  check(null, {
    'stress test success rate > 95%': () => successRate > 0.95,
  });
}

// 📊 Load test configuration for different scenarios
export const spikeOptions = {
  executor: 'ramping-arrival-rate',
  startRate: 10,
  timeUnit: '1s',
  preAllocatedVUs: 50,
  maxVUs: 200,
  stages: [
    { target: 10, duration: '2m' },
    { target: 200, duration: '30s' }, // Spike
    { target: 10, duration: '2m' },
  ],
};

export const stressOptions = {
  executor: 'ramping-vus',
  startVUs: 0,
  stages: [
    { duration: '10m', target: 200 },
    { duration: '30m', target: 200 },
    { duration: '10m', target: 0 },
  ],
};
