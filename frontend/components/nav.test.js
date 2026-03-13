import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';

test('navigation includes required admin pages', () => {
  const source = fs.readFileSync(new URL('./Nav.tsx', import.meta.url), 'utf8');
  for (const path of ['/dashboard', '/catalog', '/jobs', '/audit', '/admin-settings']) {
    assert.equal(source.includes(path), true, `missing ${path}`);
  }
});
