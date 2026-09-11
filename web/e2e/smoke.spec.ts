import { expect, test } from '@playwright/test';
import { ensureFixture, libraryDir } from './fixture';

test.describe.configure({ mode: 'serial' });
test.skip(!ensureFixture(), 'ffmpeg not installed');

test('first run to first treemap', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveURL(/\/setup$/);
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password').fill('correct horse battery');
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page).toHaveURL(/\/setup\/wizard$/);

  await page.getByLabel('Name').fill('Movies');
  await page.getByLabel('URL').fill('http://localhost:7999');
  await page.getByLabel('API key').fill('e2e');
  await page.getByRole('button', { name: 'Test' }).click();
  await expect(page.getByText(/Connected to Radarr 6\.3\.0/)).toBeVisible();
  await page.getByRole('button', { name: 'Save' }).click();
  await page.getByRole('button', { name: 'Skip, paths match' }).click();

  await page.getByLabel('Library folder as Spacearr sees it').fill(libraryDir);
  await page.getByRole('button', { name: 'Check' }).click();
  await expect(page.getByText(/Found 2 media files/)).toBeVisible();
  await page.getByRole('button', { name: 'Add folder' }).click();
  await page.getByRole('button', { name: 'Next' }).click();

  await page.getByRole('button', { name: 'Start scan' }).click();
  await expect(page.getByText('Scan complete.')).toBeVisible({ timeout: 90_000 });
  await page.getByRole('link', { name: 'Open library' }).click();

  const treemap = page.getByRole('img', { name: /Treemap of Library/ });
  await expect(treemap).toBeVisible();
  await expect(treemap).toHaveAttribute('aria-label', /2 blocks/);

  // The Kind filter sends the camelCase value the API writes (?kind=episode). Case-sensitive
  // enum binding once made both buttons a 400, and useTree's placeholderData then quietly kept
  // showing the previous tree - so check the response as well as the block count.
  const kind = page.getByRole('group', { name: 'Kind' });
  const treeFor = (k: string) => page.waitForResponse((r) => r.url().includes('/api/v1/library/tree') && r.url().includes(`kind=${k}`));
  const tv = treeFor('episode');
  await kind.getByRole('button', { name: 'TV' }).click();
  expect((await tv).status()).toBe(200);
  await expect(page.getByRole('img', { name: /Treemap of Library/ })).toHaveAttribute('aria-label', /\b0 blocks/);
  const movies = treeFor('movie');
  await kind.getByRole('button', { name: 'Movies' }).click();
  expect((await movies).status()).toBe(200);
  await expect(page.getByRole('img', { name: /Treemap of Library/ })).toHaveAttribute('aria-label', /2 blocks/);
  await kind.getByRole('button', { name: 'All' }).click();

  await page.getByRole('row', { name: /Bravo/ }).click();
  await expect(page.getByRole('region', { name: 'Details' })).toContainText('Bravo (2021)');
  await page.getByRole('button', { name: /HD-1080p/ }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('listitem')).toHaveCount(3);
  await dialog.getByRole('button', { name: 'Cancel' }).click();

  await page.getByRole('link', { name: 'Duplicates' }).click();
  await expect(page.getByText('No duplicates found')).toBeVisible();
  await page.getByRole('link', { name: 'Activity' }).click();
  await expect(page.getByRole('cell', { name: 'succeeded' }).first()).toBeVisible();
});
