import React, { useState, useEffect } from 'react';
import axios from 'axios';
import { Shield, Eye, EyeOff, Activity, Users, CreditCard, Settings } from 'lucide-react';
import './App.css';

const API_BASE_URL = 'http:

interface LoginRequest {
  email: string;
  password: string;
}

interface AuthResponse {
  token: string;
  expiresIn: number;
  tokenType: string;
  user: {
    email: string;
    role: string;
    name: string;
  };
}

interface GatewayInfo {
  title: string;
  message: string;
  version: string;
  architecture: string;
  timestamp: string;
  services: Record<string, any>;
  features: string[];
  endpoints: Record<string, string>;
  techStack: Record<string, string>;
  developerInfo: Record<string, string>;
}

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [user, setUser] = useState<AuthResponse['user'] | null>(null);
  const [showPassword, setShowPassword] = useState(false);
  const [gatewayInfo, setGatewayInfo] = useState<GatewayInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const [loginForm, setLoginForm] = useState<LoginRequest>({
    email: 'admin@paymentgateway.com',
    password: 'Admin123!'
  });

  useEffect(() => {
    loadGatewayInfo();
  }, []);

  const loadGatewayInfo = async () => {
    try {
      const response = await axios.get(`${API_BASE_URL}/`);
      setGatewayInfo(response.data);
    } catch (err) {
      console.error('Failed to load gateway info:', err);
      setError('Falha ao conectar com o servidor');
    }
  };

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');

    try {
      const response = await axios.post<AuthResponse>(`${API_BASE_URL}/api/auth/login`, loginForm);
      const { token, user } = response.data;
      
      setUser(user);
      setIsAuthenticated(true);
      
      axios.defaults.headers.common['Authorization'] = `Bearer ${token}`;
      
    } catch (err: any) {
      setError('Dados incorretos. Tente novamente.');
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = () => {
    setIsAuthenticated(false);
    setUser(null);
    delete axios.defaults.headers.common['Authorization'];
  };

  const testHealthCheck = async () => {
    try {
      const response = await axios.get(`${API_BASE_URL}/health`);
      alert(`Sistema funcionando: ${response.data.Status}`);
    } catch (err) {
      alert('Erro ao verificar sistema');
    }
  };

  if (!gatewayInfo) {
    return (
      <div className="loading-screen">
        <div className="loading-content">
          <div className="itau-logo">itaú</div>
          <div className="spinner"></div>
          <p>Carregando...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="app">
      {/* Header */}
      <header className="header">
        <div className="header-content">
          <div className="logo-section">
            <div className="itau-logo">itaú</div>
            <div className="app-info">
              <h1>Payment Gateway</h1>
              <span>Plataforma Corporativa</span>
            </div>
          </div>
          
          {isAuthenticated && user && (
            <div className="user-section">
              <div className="user-info">
                <span className="user-name">{user.name}</span>
                <span className="user-role">{user.role === 'Admin' ? 'Administrador' : 'Usuário'}</span>
              </div>
              <button onClick={handleLogout} className="btn-logout">
                Sair
              </button>
            </div>
          )}
        </div>
      </header>

      <main className="main">
        {!isAuthenticated ? (
          <div className="login-container">
            <div className="login-card">
              <div className="login-header">
                <div className="itau-logo-big">itaú</div>
                <h2>Acesso ao Sistema</h2>
                <p>Internet Banking Corporativo</p>
              </div>
              
              <form onSubmit={handleLogin} className="login-form">
                <div className="form-group">
                  <label>Usuário</label>
                  <input
                    type="email"
                    value={loginForm.email}
                    onChange={(e) => setLoginForm(prev => ({ ...prev, email: e.target.value }))}
                    placeholder="Digite seu email"
                    className="form-input"
                    required
                  />
                </div>

                <div className="form-group">
                  <label>Senha</label>
                  <div className="password-input">
                    <input
                      type={showPassword ? 'text' : 'password'}
                      value={loginForm.password}
                      onChange={(e) => setLoginForm(prev => ({ ...prev, password: e.target.value }))}
                      placeholder="Digite sua senha"
                      className="form-input"
                      required
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      className="password-toggle"
                    >
                      {showPassword ? <EyeOff size={20} /> : <Eye size={20} />}
                    </button>
                  </div>
                </div>

                {error && (
                  <div className="error-message">
                    {error}
                  </div>
                )}

                <button
                  type="submit"
                  disabled={loading}
                  className="btn-login"
                >
                  {loading ? 'Entrando...' : 'Entrar'}
                </button>
              </form>

              <div className="login-help">
                <div className="demo-credentials">
                  <p><strong>Credenciais de teste:</strong></p>
                  <p>Admin: admin@paymentgateway.com / Admin123!</p>
                  <p>User: user@paymentgateway.com / User123!</p>
                </div>
              </div>
            </div>
          </div>
        ) : (
          <div className="dashboard">
            <div className="dashboard-header">
              <h1>Painel de Controle</h1>
              <p>Sistema de Pagamentos Corporativo</p>
            </div>

            {/* Stats Cards */}
            <div className="stats-grid">
              <div className="stat-card orange">
                <div className="stat-content">
                  <div className="stat-info">
                    <span className="stat-label">Versão</span>
                    <span className="stat-value">{gatewayInfo.version}</span>
                  </div>
                  <Activity size={32} />
                </div>
              </div>

              <div className="stat-card blue">
                <div className="stat-content">
                  <div className="stat-info">
                    <span className="stat-label">Serviços</span>
                    <span className="stat-value">{Object.keys(gatewayInfo.services).length}</span>
                  </div>
                  <Users size={32} />
                </div>
              </div>

              <div className="stat-card green">
                <div className="stat-content">
                  <div className="stat-info">
                    <span className="stat-label">Features</span>
                    <span className="stat-value">{gatewayInfo.features.length}</span>
                  </div>
                  <CreditCard size={32} />
                </div>
              </div>

              <div className="stat-card purple">
                <div className="stat-content">
                  <div className="stat-info">
                    <span className="stat-label">Status</span>
                    <span className="stat-value">Online</span>
                  </div>
                  <Shield size={32} />
                </div>
              </div>
            </div>

            {/* Action Buttons */}
            <div className="actions-grid">
              <button onClick={testHealthCheck} className="action-btn green">
                <Activity size={20} />
                Verificar Sistema
              </button>

              {user?.role === 'Admin' && (
                <button className="action-btn orange">
                  <Settings size={20} />
                  Configurações
                </button>
              )}

              <button 
                onClick={() => window.open(`${API_BASE_URL}/swagger`, '_blank')}
                className="action-btn blue"
              >
                <CreditCard size={20} />
                Documentação
              </button>
            </div>

            {/* Services List */}
            <div className="services-section">
              <h3>Microserviços</h3>
              <div className="services-list">
                {Object.entries(gatewayInfo.services).map(([key, service]: [string, any]) => (
                  <div key={key} className="service-item">
                    <div className="service-info">
                      <span className="service-name">{key}</span>
                      <span className="service-desc">{service.description}</span>
                    </div>
                    <div className="service-status">
                      <span className="status-badge active">● Ativo</span>
                      {service.port && <span className="service-port">:{service.port}</span>}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}

export default App;
