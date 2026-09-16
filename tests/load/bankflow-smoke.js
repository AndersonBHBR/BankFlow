import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 10,
  duration: '20s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
  },
};

const gatewayUrl = __ENV.GATEWAY_URL || 'http://gateway:8080';

export function setup() {
  const login = http.post(`${gatewayUrl}/api/v1/auth/token`, JSON.stringify({
    login: 'admin@bankflow.local',
    password: 'BankFlow#2026',
  }), { headers: { 'Content-Type': 'application/json' } });
  check(login, { 'autenticação aprovada': (response) => response.status === 200 });
  return { token: login.json('accessToken') };
}

export default function (data) {
  const params = { headers: { Authorization: `Bearer ${data.token}` }, tags: { scenario: 'banking-read' } };
  const accounts = http.get(`${gatewayUrl}/api/v1/accounts?page=1&pageSize=20`, params);
  const transfers = http.get(`${gatewayUrl}/api/v1/transfers?page=1&pageSize=20`, params);
  check(accounts, { 'contas consultadas': (response) => response.status === 200 });
  check(transfers, { 'transferências consultadas': (response) => response.status === 200 });
  sleep(0.5);
}
