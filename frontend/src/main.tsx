import { createRoot } from 'react-dom/client';
import App from './App';
import './styles.css';

const publicAppUrl = (import.meta.env.VITE_PUBLIC_APP_URL ?? '').replace(/\/$/, '');
if (publicAppUrl && window.location.origin !== publicAppUrl) {
  window.location.replace(`${publicAppUrl}${window.location.pathname}${window.location.search}${window.location.hash}`);
} else {
  createRoot(document.getElementById('root')!).render(<App />);
}
