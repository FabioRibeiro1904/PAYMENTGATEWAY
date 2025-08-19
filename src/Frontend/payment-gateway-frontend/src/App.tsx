import React, { useState, useEffect } from 'react';
import axios from 'axios';
import * as signalR from '@microsoft/signalr';
import { Shield, Eye, EyeOff, Activity, Users, CreditCard, Settings, Send, History } from 'lucide-react';
import './App.css';

const API_GATEWAY_URL = 'http://localhost:5080';

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
    agencia: string;
    conta: string;
    balance: number;
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
  recipientType: 'email' | 'account';
  recipientEmail?: string;
  recipientAgencia?: string;
  recipientConta?: string;
  recipientName?: string;
}

interface Transaction {
  id: string;
  date: string;
  description: string;
  amount: number;
  currency: string;
  type: 'sent' | 'received';
  status: 'completed' | 'pending' | 'failed';
  recipientEmail?: string;
  senderEmail?: string;
}

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [user, setUser] = useState<AuthResponse['user'] | null>(null);
  const [showPassword, setShowPassword] = useState(false);
  const [gatewayInfo, setGatewayInfo] = useState<GatewayInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [currentView, setCurrentView] = useState<'dashboard' | 'payments' | 'settings' | 'extrato'>('dashboard');
  const [showRegister, setShowRegister] = useState(false);
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [processingTransaction, setProcessingTransaction] = useState<string | null>(null);
  const [transactionStatus, setTransactionStatus] = useState<string>('');

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
    recipientType: 'email',
    recipientEmail: '',
    recipientAgencia: '',
    recipientConta: '',
    recipientName: ''
  });

  useEffect(() => {
    loadGatewayInfo();
  }, []);

  useEffect(() => {
    if (isAuthenticated && user) {
      const setupSignalR = async () => {
        try {
          console.log('🔧 Creating SignalR connection to:', `http://localhost:5078/transactionHub`);
          
          const newConnection = new signalR.HubConnectionBuilder()
            .withUrl(`http://localhost:5078/transactionHub`)
            .configureLogging(signalR.LogLevel.Debug)
            .build();

          newConnection.onclose((error) => {
            console.log('❌ SignalR Connection Closed:', error);
          });

          newConnection.onreconnecting((error) => {
            console.log('🔄 SignalR Reconnecting:', error);
          });

          newConnection.onreconnected((connectionId) => {
            console.log('✅ SignalR Reconnected:', connectionId);
          });

          newConnection.start()
            .then(async () => {
              console.log('✅ SignalR Connected successfully!');
              console.log('📡 Connection state:', newConnection.state);
              console.log('🆔 Connection ID:', newConnection.connectionId);
              
              try {
                await newConnection.invoke('JoinUserGroup', user.email);
                console.log(`✅ Joined user group: user_${user.email}`);
              } catch (error) {
                console.error('❌ Failed to join user group:', error);
              }
              
              newConnection.on('TransactionStatusUpdated', (transactionId, status, message) => {
                console.log('🔔🔔🔔 RECEIVED SignalR Message:');
                console.log('  - Transaction ID:', transactionId);
                console.log('  - Status:', status);
                console.log('  - Message:', message);
                console.log('  - Current processing transaction:', processingTransaction);
                
                setTransactionStatus(status);
                
                if (status === 'Completed') {
                  console.log('✅ Transaction completed! Closing modal...');
                  setTimeout(async () => {
                    setProcessingTransaction(null);
                    setLoading(false);
                    alert(`✅ Transação ${transactionId} concluída com sucesso!`);
                    await loadUserBalance();
                    setCurrentView('dashboard');
                  }, 1000);
                } else if (status === 'Failed') {
                  console.log('❌ Transaction failed! Closing modal...');
                  setTimeout(() => {
                    setProcessingTransaction(null);
                    setLoading(false);
                    alert(`❌ Falha na transação ${transactionId}: ${message}`);
                  }, 1000);
                } else if (status === 'Processing') {
                  console.log('🔄 Transaction processing...');
                  setTransactionStatus('Processando pagamento... (3 segundos)');
                }
              });
            })
            .catch(err => console.error('❌ SignalR Connection Error:', err));

          setConnection(newConnection);
        } catch (err) {
          console.error('❌ SignalR Setup Error:', err);
        }
      };

      setupSignalR();
    }

    return () => {
      if (connection) {
        connection.stop();
      }
    };
  }, [isAuthenticated, user]);

  const loadGatewayInfo = async () => {
    try {
      const response = await axios.get(`${API_GATEWAY_URL}/`);
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
      const response = await axios.post<AuthResponse>(`${API_GATEWAY_URL}/api/auth/login`, loginForm);
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
      const response = await axios.get(`${API_GATEWAY_URL}/health`);
      const status = response.data?.status || response.data?.Status || 'Healthy';
      alert(`Sistema funcionando: ${status}`);
    } catch (err) {
      alert('Erro ao verificar sistema');
    }
  };

  const loadTransactions = async () => {
    try {
      setLoading(true);
      const response = await axios.get(`http://localhost:5077/api/payments/transactions/${user?.email}`, {
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        }
      });
      setTransactions(response.data);
    } catch (err) {
      console.error('Failed to load transactions:', err);
      setTransactions([]);
    } finally {
      setLoading(false);
    }
  };

  const loadUserBalance = async () => {
    try {
      if (!user?.email) return;
      
      const response = await axios.get(`http://localhost:5076/api/users/balance/${user.email}`, {
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        }
      });
      
      if (response.data?.balance !== undefined) {
        setUser(prev => prev ? { ...prev, balance: response.data.balance } : null);
        console.log('💰 Saldo atualizado:', response.data.balance);
      }
    } catch (err) {
      console.error('Failed to load user balance:', err);
    }
  };  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');

    try {
      await axios.post(`${API_GATEWAY_URL}/api/auth/register`, registerForm);
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
      if (paymentForm.amount > (user?.balance || 0)) {
        setError('Saldo insuficiente para esta transferência');
        setLoading(false);
        return;
      }

      const transferData: any = {
        fromEmail: user?.email,
        amount: paymentForm.amount
      };

      if (paymentForm.recipientType === 'email') {
        transferData.toEmail = paymentForm.recipientEmail;
      } else {
        transferData.toAgencia = paymentForm.recipientAgencia;
        transferData.toConta = paymentForm.recipientConta;
      }

      console.log('💰 Sending transfer request:', transferData);

      const response = await axios.post(`${API_GATEWAY_URL}/api/auth/transfer`, transferData, {
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('token')}`,
          'Content-Type': 'application/json'
        }
      });

      console.log('📥 Transfer response:', response.data);

      if (response.data.success) {
        console.log('🔄 Setting transaction as processing:', response.data.transactionId);
        setProcessingTransaction(response.data.transactionId);
        setTransactionStatus('Processando...');
        
        console.log('⏱️ Waiting for SignalR notification for transaction:', response.data.transactionId);
        
        setPaymentForm({ 
          amount: 0, 
          currency: 'BRL', 
          description: '', 
          recipientType: 'email',
          recipientEmail: '',
          recipientAgencia: '',
          recipientConta: '',
          recipientName: ''
        });
        
        alert('📤 Transação enviada! Aguarde a confirmação...');
      } else {
        setError('Erro ao processar transferência');
        setLoading(false);
      }
    } catch (err: any) {
      console.error('Payment error:', err);
      const errorMessage = err.response?.data?.error || 'Erro ao processar transferência';
      setError(errorMessage);
      setLoading(false);
    }
  };

  const openPayments = () => setCurrentView('payments');
  const openSettings = () => setCurrentView('settings');
  const openExtrato = () => {
    setCurrentView('extrato');
    loadTransactions();
    loadUserBalance();
  };

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
                <span className="user-account">Ag: {user?.agencia} | Conta: {user?.conta}</span>
                <span className="user-balance">Saldo: R$ {user?.balance?.toFixed(2) || '0,00'}</span>
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
                  <h1>Bem-vindo, {user?.name}</h1>
                  <p>Sistema de Transferências Bancárias</p>
                  <div className="account-info">
                    <div className="account-details">
                      <span>Agência: <strong>{user?.agencia}</strong></span>
                      <span>Conta: <strong>{user?.conta}</strong></span>
                      <span>Saldo: <strong>R$ {user?.balance?.toFixed(2) || '0,00'}</strong></span>
                    </div>
                  </div>
                </div>

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

                <div className="actions-grid">
                  <button onClick={openPayments} className="action-btn green">
                    <Send size={20} />
                    Nova Transferência
                  </button>

                  <button onClick={openExtrato} className="action-btn blue">
                    <History size={20} />
                    Extrato
                  </button>

                  {user?.role === 'Admin' && (
                    <>
                      <button onClick={testHealthCheck} className="action-btn orange">
                        <Activity size={20} />
                        Verificar Sistema
                      </button>

                      <button onClick={openSettings} className="action-btn purple">
                        <Settings size={20} />
                        Configurações
                      </button>

                      <button 
                        onClick={() => window.open(`${API_GATEWAY_URL}/swagger/index.html`, '_blank')}
                        className="action-btn gray"
                      >
                        <CreditCard size={20} />
                        Documentação
                      </button>
                    </>
                  )}
                </div>

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
                          <span className="service-port">:5080</span>
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
                  <h2>Nova Transferência</h2>
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
                      <label>Tipo de Destinatário</label>
                      <select
                        value={paymentForm.recipientType}
                        onChange={(e) => setPaymentForm(prev => ({ 
                          ...prev, 
                          recipientType: e.target.value as 'email' | 'account',
                          recipientEmail: '',
                          recipientAgencia: '',
                          recipientConta: '',
                          recipientName: ''
                        }))}
                        className="form-input"
                      >
                        <option value="email">Por Email</option>
                        <option value="account">Por Conta Bancária</option>
                      </select>
                    </div>

                    {paymentForm.recipientType === 'email' ? (
                      <div className="form-group">
                        <label>Email do Destinatário</label>
                        <input
                          type="email"
                          value={paymentForm.recipientEmail || ''}
                          onChange={(e) => setPaymentForm(prev => ({ ...prev, recipientEmail: e.target.value }))}
                          placeholder="destinatario@email.com"
                          className="form-input"
                          required
                        />
                      </div>
                    ) : (
                      <>
                        <div className="form-row">
                          <div className="form-group">
                            <label>Agência</label>
                            <input
                              type="text"
                              value={paymentForm.recipientAgencia || ''}
                              onChange={(e) => setPaymentForm(prev => ({ ...prev, recipientAgencia: e.target.value }))}
                              placeholder="12345-6"
                              className="form-input"
                              maxLength={7}
                              required
                            />
                          </div>

                          <div className="form-group">
                            <label>Conta</label>
                            <input
                              type="text"
                              value={paymentForm.recipientConta || ''}
                              onChange={(e) => setPaymentForm(prev => ({ ...prev, recipientConta: e.target.value }))}
                              placeholder="12345678-90"
                              className="form-input"
                              maxLength={11}
                              required
                            />
                          </div>
                        </div>

                        <div className="form-group">
                          <label>Nome do Titular (opcional)</label>
                          <input
                            type="text"
                            value={paymentForm.recipientName || ''}
                            onChange={(e) => setPaymentForm(prev => ({ ...prev, recipientName: e.target.value }))}
                            placeholder="Nome para confirmação"
                            className="form-input"
                          />
                        </div>
                      </>
                    )}

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

                    <div className="payment-summary">
                      <div className="summary-row">
                        <span>Valor a transferir:</span>
                        <span>R$ {paymentForm.amount.toFixed(2)}</span>
                      </div>
                      <div className="summary-row">
                        <span>Saldo atual:</span>
                        <span>R$ {user?.balance?.toFixed(2) || '0,00'}</span>
                      </div>
                      <div className="summary-row total">
                        <span>Saldo após transferência:</span>
                        <span>R$ {((user?.balance || 0) - paymentForm.amount).toFixed(2)}</span>
                      </div>
                    </div>

                    <button
                      type="submit"
                      disabled={loading || paymentForm.amount <= 0 || paymentForm.amount > (user?.balance || 0)}
                      className="btn-payment"
                    >
                      {loading ? 'Processando...' : 'Processar Transferência'}
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

            {currentView === 'extrato' && (
              <div className="extrato-section">
                <div className="section-header">
                  <h2>Extrato de Transações</h2>
                  <button onClick={() => setCurrentView('dashboard')} className="btn-back">
                    Voltar ao Dashboard
                  </button>
                </div>

                <div className="extrato-card">
                  <div className="extrato-header">
                    <h3>Histórico de Transferências</h3>
                    <div className="extrato-filters">
                      <select className="form-input">
                        <option value="all">Todas as transações</option>
                        <option value="sent">Enviadas</option>
                        <option value="received">Recebidas</option>
                      </select>
                      <input type="date" className="form-input" />
                    </div>
                  </div>

                  <div className="transactions-list">
                    {loading ? (
                      <div className="loading-message">
                        Carregando transações...
                      </div>
                    ) : transactions.length === 0 ? (
                      <div className="empty-message">
                        Nenhuma transação encontrada
                      </div>
                    ) : (
                      transactions.map((transaction) => (
                        <div key={transaction.id} className="transaction-item">
                          <div className="transaction-info">
                            <span className="transaction-date">
                              {new Date(transaction.date).toLocaleString('pt-BR')}
                            </span>
                            <span className="transaction-desc">{transaction.description}</span>
                          </div>
                          <div className={`transaction-amount ${transaction.type}`}>
                            {transaction.amount > 0 ? '+' : ''}R$ {Math.abs(transaction.amount).toFixed(2)}
                          </div>
                          <div className="transaction-status">
                            <span className={`status-badge ${transaction.status}`}>
                              {transaction.status === 'completed' ? 'Concluído' : 
                               transaction.status === 'pending' ? 'Pendente' : 'Falhou'}
                            </span>
                          </div>
                        </div>
                      ))
                    )}
                  </div>

                  <div className="extrato-summary">
                    <div className="summary-item">
                      <span className="summary-label">Total Enviado (7 dias):</span>
                      <span className="summary-value sent">
                        R$ {transactions
                          .filter(t => t.type === 'sent' && t.status === 'completed')
                          .reduce((sum, t) => sum + Math.abs(t.amount), 0)
                          .toFixed(2)}
                      </span>
                    </div>
                    <div className="summary-item">
                      <span className="summary-label">Total Recebido (7 dias):</span>
                      <span className="summary-value received">
                        R$ {transactions
                          .filter(t => t.type === 'received' && t.status === 'completed')
                          .reduce((sum, t) => sum + t.amount, 0)
                          .toFixed(2)}
                      </span>
                    </div>
                    <div className="summary-item">
                      <span className="summary-label">Saldo Líquido:</span>
                      <span className="summary-value">
                        R$ {transactions
                          .filter(t => t.status === 'completed')
                          .reduce((sum, t) => sum + t.amount, 0)
                          .toFixed(2)}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>
        )}
      </main>

      {processingTransaction && (
        <div className="transaction-modal">
          <div className="transaction-modal-content">
            <div className="transaction-loading">
              <div className="spinner"></div>
              <h3>Processando Transferência</h3>
              <p>Status: {transactionStatus}</p>
              <p>ID da Transação: {processingTransaction}</p>
              <p>🔄 Aguardando confirmação via Kafka + Redis...</p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default App;
