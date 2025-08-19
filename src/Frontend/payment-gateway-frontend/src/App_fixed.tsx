import React, { useState, useEffect } from 'react';
import axios from 'axios';
import { Shield, Eye, EyeOff, Activity, Users, CreditCard, Settings, UserPlus, Send, History } from 'lucide-react';
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
  features: string[];
}

interface CreateUserRequest {
  name: string;
  email: string;
  password: string;
  role: string;
}

interface PaymentRequest {
  amount: number;
  currency: string;
  description: string;
  recipientEmail: string;
}

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [user, setUser] = useState<AuthResponse['user'] | null>(null);
  const [showPassword, setShowPassword] = useState(false);
  const [gatewayInfo, setGatewayInfo] = useState<GatewayInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [currentView, setCurrentView] = useState<'dashboard' | 'payments' | 'settings'>('dashboard');
  const [showRegister, setShowRegister] = useState(false);

  const [loginForm, setLoginForm] = useState<LoginRequest>({
    email: '',
    password: ''
  });

  const [registerForm, setRegisterForm] = useState<CreateUserRequest>({
    name: '',
    email: '',
    password: '',
    role: 'User'
  });

  const [paymentForm, setPaymentForm] = useState<PaymentRequest>({
    amount: 0,
    currency: 'BRL',
    description: '',
    recipientEmail: ''
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
    setCurrentView('dashboard');
    delete axios.defaults.headers.common['Authorization'];
  };

  const testHealthCheck = async () => {
    try {
      const response = await axios.get(`${API_BASE_URL}/health`);
      const status = response.data?.status || response.data?.Status || 'Healthy';
      alert(`Sistema funcionando: ${status}`);
    } catch (err) {
      alert('Erro ao verificar sistema');
    }
  };

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');

    try {
      await axios.post(`${API_BASE_URL}/api/auth/register`, registerForm);
      alert('Usuário criado com sucesso!');
      setShowRegister(false);
      setRegisterForm({ name: '', email: '', password: '', role: 'User' });
    } catch (err: any) {
      setError(err.response?.data?.error || 'Erro ao criar usuário');
    } finally {
      setLoading(false);
    }
  };

  const handlePayment = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');

    try {
      const response = await axios.post(`${API_BASE_URL}/api/payments/process`, paymentForm);
      alert(`Pagamento processado com sucesso! ID: ${response.data.paymentId}`);
      setPaymentForm({ amount: 0, currency: 'BRL', description: '', recipientEmail: '' });
      setCurrentView('dashboard');
    } catch (err: any) {
      setError(err.response?.data?.error || 'Erro ao processar pagamento');
    } finally {
      setLoading(false);
    }
  };

  const openPayments = () => setCurrentView('payments');
  const openSettings = () => setCurrentView('settings');

  return (
    <div className="app">
      <header className="header">
        <div className="header-content">
          <div className="logo-section">
            <div className="itau-logo">itaú</div>
            <div className="system-title">
              <span className="title">Payment Gateway</span>
              <span className="subtitle">Fabio Lucio Ribeiro</span>
            </div>
          </div>
          
          {isAuthenticated && (
            <div className="header-user">
              <div className="user-info">
                <span className="user-name">{user?.name}</span>
                <span className="user-role">{user?.role === 'Admin' ? 'Administrador' : 'Usuário'}</span>
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
                <h2>{showRegister ? 'Criar Conta' : 'Acesso ao Sistema'}</h2>
                <p>Internet Banking Corporativo</p>
              </div>
              
              {!showRegister ? (
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

                  <div className="login-actions">
                    <button
                      type="button"
                      onClick={() => setShowRegister(true)}
                      className="btn-register"
                    >
                      Criar nova conta
                    </button>
                  </div>
                </form>
              ) : (
                <form onSubmit={handleRegister} className="login-form">
                  <div className="form-group">
                    <label>Nome Completo</label>
                    <input
                      type="text"
                      value={registerForm.name}
                      onChange={(e) => setRegisterForm(prev => ({ ...prev, name: e.target.value }))}
                      placeholder="Digite seu nome completo"
                      className="form-input"
                      required
                    />
                  </div>

                  <div className="form-group">
                    <label>Email</label>
                    <input
                      type="email"
                      value={registerForm.email}
                      onChange={(e) => setRegisterForm(prev => ({ ...prev, email: e.target.value }))}
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
                        value={registerForm.password}
                        onChange={(e) => setRegisterForm(prev => ({ ...prev, password: e.target.value }))}
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

                  <div className="form-group">
                    <label>Tipo de Conta</label>
                    <select
                      value={registerForm.role}
                      onChange={(e) => setRegisterForm(prev => ({ ...prev, role: e.target.value }))}
                      className="form-input"
                    >
                      <option value="User">Usuário</option>
                      <option value="Admin">Administrador</option>
                    </select>
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
                    {loading ? 'Criando...' : 'Criar Conta'}
                  </button>

                  <div className="login-actions">
                    <button
                      type="button"
                      onClick={() => setShowRegister(false)}
                      className="btn-back"
                    >
                      Voltar ao Login
                    </button>
                  </div>
                </form>
              )}

              <div className="login-help">
                <p>Sistema seguro com autenticação JWT</p>
              </div>
            </div>
          </div>
        ) : (
          <div className="dashboard">
            {currentView === 'dashboard' && (
              <>
                <div className="dashboard-header">
                  <h1>Painel de Controle</h1>
                  <p>Sistema de Pagamentos Corporativo</p>
                </div>

                {/* Stats Cards - Apenas para Admin */}
                {user?.role === 'Admin' && (
                  <div className="stats-grid">
                    <div className="stat-card orange">
                      <div className="stat-content">
                        <div className="stat-info">
                          <span className="stat-label">Versão</span>
                          <span className="stat-value">{gatewayInfo?.version || '1.0.0'}</span>
                        </div>
                        <Activity size={32} />
                      </div>
                    </div>

                    <div className="stat-card blue">
                      <div className="stat-content">
                        <div className="stat-info">
                          <span className="stat-label">Microserviços</span>
                          <span className="stat-value">4</span>
                        </div>
                        <Users size={32} />
                      </div>
                    </div>

                    <div className="stat-card green">
                      <div className="stat-content">
                        <div className="stat-info">
                          <span className="stat-label">Features</span>
                          <span className="stat-value">{gatewayInfo?.features?.length || 9}</span>
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
                )}

                {/* Action Buttons */}
                <div className="actions-grid">
                  <button onClick={openPayments} className="action-btn green">
                    <Send size={20} />
                    Processar Pagamento
                  </button>

                  <button onClick={testHealthCheck} className="action-btn blue">
                    <Activity size={20} />
                    Verificar Sistema
                  </button>

                  {user?.role === 'Admin' && (
                    <button onClick={openSettings} className="action-btn orange">
                      <Settings size={20} />
                      Configurações
                    </button>
                  )}

                  <button 
                    onClick={() => window.open(`${API_BASE_URL}/swagger/index.html`, '_blank')}
                    className="action-btn purple"
                  >
                    <CreditCard size={20} />
                    Documentação
                  </button>
                </div>

                {/* Services List - Apenas para Admin */}
                {user?.role === 'Admin' && (
                  <div className="services-section">
                    <h3>Microserviços</h3>
                    <div className="services-list">
                      <div className="service-item">
                        <div className="service-info">
                          <span className="service-name">PaymentGateway.ApiGateway</span>
                          <span className="service-desc">API Gateway principal com YARP</span>
                        </div>
                        <div className="service-status">
                          <span className="status-badge active">● Ativo</span>
                          <span className="service-port">:5000</span>
                        </div>
                      </div>
                      <div className="service-item">
                        <div className="service-info">
                          <span className="service-name">PaymentGateway.Users</span>
                          <span className="service-desc">Serviço de gerenciamento de usuários</span>
                        </div>
                        <div className="service-status">
                          <span className="status-badge active">● Ativo</span>
                          <span className="service-port">:5076</span>
                        </div>
                      </div>
                      <div className="service-item">
                        <div className="service-info">
                          <span className="service-name">PaymentGateway.Payments</span>
                          <span className="service-desc">Serviço de processamento de pagamentos</span>
                        </div>
                        <div className="service-status">
                          <span className="status-badge active">● Ativo</span>
                          <span className="service-port">:5077</span>
                        </div>
                      </div>
                      <div className="service-item">
                        <div className="service-info">
                          <span className="service-name">PaymentGateway.Notifications</span>
                          <span className="service-desc">Serviço de notificações</span>
                        </div>
                        <div className="service-status">
                          <span className="status-badge active">● Ativo</span>
                          <span className="service-port">:5078</span>
                        </div>
                      </div>
                    </div>
                  </div>
                )}
              </>
            )}

            {currentView === 'payments' && (
              <div className="payments-section">
                <div className="section-header">
                  <h2>Processar Pagamento</h2>
                  <button onClick={() => setCurrentView('dashboard')} className="btn-back">
                    Voltar ao Dashboard
                  </button>
                </div>

                <div className="payment-card">
                  <form onSubmit={handlePayment} className="payment-form">
                    <div className="form-row">
                      <div className="form-group">
                        <label>Valor</label>
                        <input
                          type="number"
                          step="0.01"
                          value={paymentForm.amount}
                          onChange={(e) => setPaymentForm(prev => ({ ...prev, amount: parseFloat(e.target.value) }))}
                          placeholder="0.00"
                          className="form-input"
                          required
                        />
                      </div>

                      <div className="form-group">
                        <label>Moeda</label>
                        <select
                          value={paymentForm.currency}
                          onChange={(e) => setPaymentForm(prev => ({ ...prev, currency: e.target.value }))}
                          className="form-input"
                        >
                          <option value="BRL">BRL - Real</option>
                          <option value="USD">USD - Dólar</option>
                          <option value="EUR">EUR - Euro</option>
                        </select>
                      </div>
                    </div>

                    <div className="form-group">
                      <label>Email do Destinatário</label>
                      <input
                        type="email"
                        value={paymentForm.recipientEmail}
                        onChange={(e) => setPaymentForm(prev => ({ ...prev, recipientEmail: e.target.value }))}
                        placeholder="destinatario@email.com"
                        className="form-input"
                        required
                      />
                    </div>

                    <div className="form-group">
                      <label>Descrição</label>
                      <textarea
                        value={paymentForm.description}
                        onChange={(e) => setPaymentForm(prev => ({ ...prev, description: e.target.value }))}
                        placeholder="Descrição do pagamento"
                        className="form-input"
                        rows={3}
                        required
                      />
                    </div>

                    {error && (
                      <div className="error-message">
                        {error}
                      </div>
                    )}

                    <button
                      type="submit"
                      disabled={loading}
                      className="btn-payment"
                    >
                      {loading ? 'Processando...' : 'Processar Pagamento'}
                    </button>
                  </form>
                </div>
              </div>
            )}

            {currentView === 'settings' && user?.role === 'Admin' && (
              <div className="settings-section">
                <div className="section-header">
                  <h2>Configurações do Sistema</h2>
                  <button onClick={() => setCurrentView('dashboard')} className="btn-back">
                    Voltar ao Dashboard
                  </button>
                </div>

                <div className="settings-card">
                  <h3>Criar Novo Usuário</h3>
                  <form onSubmit={handleRegister} className="settings-form">
                    <div className="form-row">
                      <div className="form-group">
                        <label>Nome</label>
                        <input
                          type="text"
                          value={registerForm.name}
                          onChange={(e) => setRegisterForm(prev => ({ ...prev, name: e.target.value }))}
                          placeholder="Nome completo"
                          className="form-input"
                          required
                        />
                      </div>

                      <div className="form-group">
                        <label>Email</label>
                        <input
                          type="email"
                          value={registerForm.email}
                          onChange={(e) => setRegisterForm(prev => ({ ...prev, email: e.target.value }))}
                          placeholder="Email do usuário"
                          className="form-input"
                          required
                        />
                      </div>
                    </div>

                    <div className="form-row">
                      <div className="form-group">
                        <label>Senha</label>
                        <input
                          type="password"
                          value={registerForm.password}
                          onChange={(e) => setRegisterForm(prev => ({ ...prev, password: e.target.value }))}
                          placeholder="Senha do usuário"
                          className="form-input"
                          required
                        />
                      </div>

                      <div className="form-group">
                        <label>Tipo</label>
                        <select
                          value={registerForm.role}
                          onChange={(e) => setRegisterForm(prev => ({ ...prev, role: e.target.value }))}
                          className="form-input"
                        >
                          <option value="User">Usuário</option>
                          <option value="Admin">Administrador</option>
                        </select>
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
                      className="btn-create-user"
                    >
                      {loading ? 'Criando...' : 'Criar Usuário'}
                    </button>
                  </form>
                </div>
              </div>
            )}
          </div>
        )}
      </main>
    </div>
  );
}

export default App;
