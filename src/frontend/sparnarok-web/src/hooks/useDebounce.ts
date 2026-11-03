import { useState, useEffect } from 'react';

/**
 * Hook para retrasar la actualización de un valor hasta que haya transcurrido
 * un lapso determinado sin nuevos cambios (Debounce ~300ms).
 * Evita la saturación del backend mientras el usuario escribe en inputs de búsqueda.
 *
 * @param value Valor reactivo a debouncear.
 * @param delay Retardo en milisegundos (por defecto 300ms).
 * @returns El valor estabilizado tras el retardo.
 */
export function useDebounce<T>(value: T, delay: number = 300): T {
  const [debouncedValue, setDebouncedValue] = useState<T>(value);

  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedValue(value);
    }, delay);

    return () => {
      clearTimeout(handler);
    };
  }, [value, delay]);

  return debouncedValue;
}
