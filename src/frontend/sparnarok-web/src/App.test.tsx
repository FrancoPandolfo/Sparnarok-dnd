import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import App from './App';
import i18n from './i18n';

describe('Sparnarok Codex App with i18n', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('es');
  });

  it('renders the initial localized Spanish title and interactive counter', () => {
    render(<App />);

    expect(
      screen.getByRole('heading', { level: 1, name: /Sparnarok: El Códice del Arquitecto/i })
    ).toBeInTheDocument();

    expect(
      screen.getByRole('button', { name: /Simulaciones ejecutadas: 0/i })
    ).toBeInTheDocument();
  });

  it('increments the simulation counter upon user interaction', () => {
    render(<App />);

    const counterButton = screen.getByRole('button', { name: /Simulaciones ejecutadas: 0/i });
    fireEvent.click(counterButton);

    expect(
      screen.getByRole('button', { name: /Simulaciones ejecutadas: 1/i })
    ).toBeInTheDocument();
  });

  it('switches application language dynamically when toggling the language button', async () => {
    render(<App />);

    const langToggleBtn = screen.getByRole('button', { name: /Cambiar a Inglés/i });
    fireEvent.click(langToggleBtn);

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: /Sparnarok: The Architect's Codex/i,
      })
    ).toBeInTheDocument();

    expect(
      screen.getByRole('button', { name: /Switch to Spanish/i })
    ).toBeInTheDocument();
  });
});
