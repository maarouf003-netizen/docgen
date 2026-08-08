import '@testing-library/jest-dom/vitest';

// ProseMirror (TipTap) requires geometry APIs on Text nodes and Range that
// jsdom does not implement. Without these, editor updates throw
// "target.getClientRects is not a function".

const emptyRects = (): DOMRectList => [] as unknown as DOMRectList;

const zeroRect = (): DOMRect =>
  ({
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    width: 0,
    height: 0,
    x: 0,
    y: 0,
    toJSON: () => ({}),
  }) as unknown as DOMRect;

if (typeof (Text.prototype as { getClientRects?: unknown }).getClientRects !== 'function') {
  (Text.prototype as unknown as { getClientRects(): DOMRectList }).getClientRects = emptyRects;
}
if (typeof (Text.prototype as { getBoundingClientRect?: unknown }).getBoundingClientRect !== 'function') {
  (Text.prototype as unknown as { getBoundingClientRect(): DOMRect }).getBoundingClientRect = zeroRect;
}
if (typeof Range.prototype.getClientRects !== 'function') {
  (Range.prototype as unknown as { getClientRects(): DOMRectList }).getClientRects = emptyRects;
}
if (typeof Range.prototype.getBoundingClientRect !== 'function') {
  (Range.prototype as unknown as { getBoundingClientRect(): DOMRect }).getBoundingClientRect = zeroRect;
}
// jsdom lacks document.elementFromPoint; ProseMirror calls it during click handling.
if (typeof document.elementFromPoint !== 'function') {
  (document as unknown as { elementFromPoint(x: number, y: number): Element | null }).elementFromPoint =
    () => null;
}
