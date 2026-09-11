import { afterEach, describe, expect, it, vi } from 'vitest';
import { downloadBlob } from './download';

describe('downloadBlob', () => {
  afterEach(() => vi.restoreAllMocks());

  it('ينشئ رابطًا باسم الملف المُجرَّد، ينقر عليه، يزيله ثم يحرّر الـ URL', () => {
    const blob = new Blob(['data'], { type: 'application/octet-stream' });
    const createObjectURL = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test');
    const revokeObjectURL = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    const appendSpy = vi.spyOn(document.body, 'appendChild');

    downloadBlob(blob, '  مثال.xlsx  ');

    expect(createObjectURL).toHaveBeenCalledWith(blob);
    expect(appendSpy).toHaveBeenCalledTimes(1);
    const anchor = appendSpy.mock.calls[0][0] as HTMLAnchorElement;
    expect(anchor.href).toBe('blob:test');
    expect(anchor.download).toBe('مثال.xlsx');
    expect(click).toHaveBeenCalledTimes(1);
    expect(anchor.parentElement).toBeNull();
    expect(revokeObjectURL).toHaveBeenCalledTimes(1);
  });

  it('يرمي خطأً صريحًا عندما يكون اسم الملف فارغًا أو فراغات بعد التضميم', () => {
    const blob = new Blob(['x']);
    expect(() => downloadBlob(blob, '')).toThrow('downloadBlob');
    expect(() => downloadBlob(blob, '   ')).toThrow('downloadBlob');
  });
});