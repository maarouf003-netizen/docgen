/**
 * أنواع `e2e-safety-guard.mjs` لاستهلاكه من `playwright.config.ts` أثناء `tsc -b`.
 */
export interface E2eTarget {
  host: string;
  staging: boolean;
}

export interface E2eDatabaseTarget {
  host: string;
  port: string;
  database: string;
  format: 'uri' | 'keyword';
}

export interface SafetySelfTestCase {
  name: string;
  run: () => unknown;
  expect?: Record<string, unknown>;
  throws?: RegExp;
}

export function stagingAllowed(env?: Record<string, string | undefined>): boolean;

export function assertSafeE2eTarget(
  baseUrl: string,
  env?: Record<string, string | undefined>,
): E2eTarget;

export function parseE2eConnectionString(connectionString: string): E2eDatabaseTarget;

export function assertSafeE2eDatabase(
  connectionString: string,
  env?: Record<string, string | undefined>,
): E2eDatabaseTarget;

export function safetySelfTestCases(): SafetySelfTestCase[];

export function runSelfTest(): number;
