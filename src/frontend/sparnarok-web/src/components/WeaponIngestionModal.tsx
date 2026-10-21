import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { IngestionResultDto } from '../types/dnd';

export interface WeaponIngestionModalProps {
  isOpen: boolean;
  onClose: () => void;
  onUpload: (file: File) => Promise<IngestionResultDto>;
}

/**
 * Modal para la ingesta masiva de catálogos de armas a través de archivos delimitados por comas (CSV).
 */
export const WeaponIngestionModal: React.FC<WeaponIngestionModalProps> = ({
  isOpen,
  onClose,
  onUpload,
}) => {
  const { t } = useTranslation();

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [result, setResult] = useState<IngestionResultDto | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      const file = e.target.files[0];
      setSelectedFile(file);
      setResult(null);
      setErrorMessage(null);
    }
  };

  const handleUploadSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedFile) {
      setErrorMessage(t('ingestion.noFileSelected'));
      return;
    }

    setIsUploading(true);
    setErrorMessage(null);

    try {
      const response = await onUpload(selectedFile);
      setResult(response);
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : t('ingestion.errorUploading'));
    } finally {
      setIsUploading(false);
    }
  };

  const handleClose = () => {
    setSelectedFile(null);
    setResult(null);
    setErrorMessage(null);
    onClose();
  };

  return (
    <div className="modal-backdrop" onClick={handleClose} role="dialog" aria-modal="true">
      <div className="modal-content form-modal fade-in" onClick={(e) => e.stopPropagation()}>
        <header className="modal-header">
          <h2 className="modal-title">📥 {t('ingestion.title')}</h2>
          <button
            type="button"
            id="close-ingestion-modal-btn"
            className="modal-close-btn"
            onClick={handleClose}
            aria-label={t('bestiary.form.cancel')}
          >
            ✕
          </button>
        </header>

        <p className="ingestion-desc">{t('ingestion.subtitle')}</p>

        {/* Guía de formato de columnas CSV requeridas */}
        <div className="csv-spec-box">
          <span className="spec-title">📋 {t('ingestion.specTitle')}</span>
          <code className="spec-code">
            Name,Damage,DamageType,Properties,Weight,Cost
          </code>
          <p className="spec-example">
            {t('ingestion.specExample')}: <code>Longsword,1d8,Slashing,"Versatile (1d10)",3.0,15 gp</code>
          </p>
        </div>

        {errorMessage && <div className="form-error-banner">{errorMessage}</div>}

        <form onSubmit={handleUploadSubmit} className="ingestion-form">
          <div className="file-drop-zone">
            <input
              id="weapon-csv-input"
              type="file"
              accept=".csv"
              onChange={handleFileChange}
              className="file-native-input"
            />
            <label htmlFor="weapon-csv-input" className="file-custom-label">
              <span className="file-icon">📄</span>
              <span className="file-prompt">
                {selectedFile ? selectedFile.name : t('ingestion.chooseFilePrompt')}
              </span>
              <span className="file-subprompt">(.csv)</span>
            </label>
          </div>

          <footer className="form-actions-footer">
            <button
              type="button"
              className="btn-cancel"
              onClick={handleClose}
              disabled={isUploading}
            >
              {t('bestiary.form.cancel')}
            </button>
            <button
              type="submit"
              id="upload-csv-btn"
              className="btn-submit-gold"
              disabled={!selectedFile || isUploading}
            >
              {isUploading ? t('ingestion.uploading') : t('ingestion.uploadButton')}
            </button>
          </footer>
        </form>

        {/* Panel de resultados de ingesta */}
        {result && (
          <div className="ingestion-results-card fade-in" id="ingestion-results-panel">
            <h4 className="results-title">✅ {t('ingestion.resultsTitle')}</h4>
            <div className="results-metrics-grid">
              <div className="metric-badge total">
                <span className="metric-label">{t('ingestion.metricTotal')}</span>
                <span className="metric-value" id="ingestion-total">{result.totalProcessed}</span>
              </div>
              <div className="metric-badge inserted">
                <span className="metric-label">{t('ingestion.metricInserted')}</span>
                <span className="metric-value" id="ingestion-inserted">{result.inserted}</span>
              </div>
              <div className="metric-badge updated">
                <span className="metric-label">{t('ingestion.metricUpdated')}</span>
                <span className="metric-value" id="ingestion-updated">{result.updated}</span>
              </div>
            </div>

            {result.errors.length > 0 && (
              <div className="ingestion-errors-list">
                <span className="errors-heading">⚠️ {t('ingestion.errorsEncountered')}:</span>
                <ul>
                  {result.errors.map((err, idx) => (
                    <li key={idx}>{err}</li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
