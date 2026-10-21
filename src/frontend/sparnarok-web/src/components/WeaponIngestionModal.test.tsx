import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WeaponIngestionModal } from './WeaponIngestionModal';
import i18n from '../i18n';
import type { IngestionResultDto } from '../types/dnd';

describe('WeaponIngestionModal Component Suite', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('es');
  });

  const mockOnClose = vi.fn();
  const mockOnUpload = vi.fn();

  const renderModal = (isOpen = true) => {
    return render(
      <WeaponIngestionModal
        isOpen={isOpen}
        onClose={mockOnClose}
        onUpload={mockOnUpload}
      />
    );
  };

  it('does not render when isOpen is false', () => {
    renderModal(false);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders modal dialog with CSV requirements when isOpen is true', () => {
    renderModal(true);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText(/Pipeline de Ingesta: Armas/i)).toBeInTheDocument();
    expect(screen.getByText(/Name,Damage,DamageType,Properties,Weight,Cost/i)).toBeInTheDocument();
  });

  it('handles CSV file selection and enables submit button', async () => {
    renderModal(true);

    const submitBtn = screen.getByRole('button', { name: /Iniciar Ingesta/i });
    expect(submitBtn).toBeDisabled();

    const file = new File(
      ['Name,Damage,DamageType,Properties,Weight,Cost\nHalberd,1d10,Slashing,"Heavy, Reach",6.0,20 gp'],
      'weapons.csv',
      { type: 'text/csv' }
    );

    const fileInput = screen.getByLabelText(/weapons.csv|Haz clic o arrastra/i);
    await userEvent.upload(fileInput, file);

    expect(screen.getByText('weapons.csv')).toBeInTheDocument();
    expect(submitBtn).not.toBeDisabled();
  });

  it('uploads CSV file and displays ingestion metrics on success', async () => {
    const successResult: IngestionResultDto = {
      totalProcessed: 2,
      inserted: 1,
      updated: 1,
      errors: [],
    };
    mockOnUpload.mockResolvedValueOnce(successResult);

    renderModal(true);

    const file = new File(
      ['Name,Damage,DamageType,Properties,Weight,Cost\nShortsword,1d6,Piercing,"Finesse, Light",2.0,10 gp'],
      'sample.csv',
      { type: 'text/csv' }
    );

    const fileInput = screen.getByLabelText(/sample.csv|Haz clic o arrastra/i);
    await userEvent.upload(fileInput, file);

    const submitBtn = screen.getByRole('button', { name: /Iniciar Ingesta/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(mockOnUpload).toHaveBeenCalledWith(file);
      expect(screen.getByText(/Resumen de Ingesta/i)).toBeInTheDocument();
      expect(screen.getByText('2')).toBeInTheDocument(); // totalProcessed
      expect(screen.getAllByText('1').length).toBeGreaterThanOrEqual(2); // inserted & updated
    });
  });

  it('displays error message when upload fails', async () => {
    mockOnUpload.mockRejectedValueOnce(new Error('Formato de CSV inválido'));

    renderModal(true);

    const file = new File(['invalid'], 'bad.csv', { type: 'text/csv' });
    const fileInput = screen.getByLabelText(/bad.csv|Haz clic o arrastra/i);
    await userEvent.upload(fileInput, file);

    const submitBtn = screen.getByRole('button', { name: /Iniciar Ingesta/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(screen.getByText('Formato de CSV inválido')).toBeInTheDocument();
    });
  });
});
