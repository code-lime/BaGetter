const childProcess = require('child_process');
const fs = require('fs');
const http = require('http');
const https = require('https');
const os = require('os');
const path = require('path');

function input(name, required = false) {
  const key = `INPUT_${name.replace(/ /g, '_').replace(/-/g, '_').toUpperCase()}`;
  const value = process.env[key] || '';
  if (required && !value.trim()) {
    throw new Error(`Input '${name}' is required`);
  }

  return value;
}

function appendFile(envName, line) {
  const file = process.env[envName];
  if (file) {
    fs.appendFileSync(file, `${line}${os.EOL}`);
  }
}

function setOutput(name, value) {
  appendFile('GITHUB_OUTPUT', `${name}=${value}`);
}

function saveState(name, value) {
  appendFile('GITHUB_STATE', `${name}=${value}`);
}

function get(url) {
  return new Promise((resolve) => {
    const client = url.startsWith('https:') ? https : http;
    const request = client.get(url, (response) => {
      response.resume();
      resolve(response.statusCode >= 200 && response.statusCode < 500);
    });

    request.on('error', () => resolve(false));
    request.setTimeout(1000, () => {
      request.destroy();
      resolve(false);
    });
  });
}

function isRunning(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

async function waitForServer(pid, sourceUrl, logFile) {
  for (let attempt = 0; attempt < 60; attempt++) {
    if (await get(sourceUrl)) {
      return;
    }

    if (!isRunning(pid)) {
      const log = fs.existsSync(logFile) ? fs.readFileSync(logFile, 'utf8') : '';
      throw new Error(`BaGetter exited before becoming ready.${os.EOL}${log}`);
    }

    await new Promise((resolve) => setTimeout(resolve, 1000));
  }

  const log = fs.existsSync(logFile) ? fs.readFileSync(logFile, 'utf8') : '';
  throw new Error(`BaGetter did not become ready in time.${os.EOL}${log}`);
}

async function main() {
  const actionPath = process.env.GITHUB_ACTION_PATH || process.cwd();
  const runnerTemp = process.env.RUNNER_TEMP || os.tmpdir();
  const stateDir = path.join(runnerTemp, 'bagetter-action');
  fs.mkdirSync(stateDir, { recursive: true });

  const url = input('url') || 'http://127.0.0.1:5050';
  const apiKey = input('api-key') || 'github-actions';
  const owner = input('owner', true);
  const repository = input('repository', true);
  const token = input('token', true);
  const branch = input('branch');
  const rootPath = input('root-path');
  const apiBaseUrl = input('api-base-url') || 'https://api.github.com';
  const databasePath = input('database-path') || path.join(stateDir, 'bagetter.db');

  const sourceUrl = `${url.replace(/\/+$/, '')}/v3/index.json`;
  const logFile = path.join(stateDir, 'server.log');
  const out = fs.openSync(logFile, 'a');
  const serverProject = path.join(actionPath, 'src', 'BaGetter', 'BaGetter.csproj');

  const env = {
    ...process.env,
    ApiKey: apiKey,
    Database__Type: 'Sqlite',
    Database__ConnectionString: `Data Source=${databasePath}`,
    Search__Type: 'Database',
    Storage__Type: 'GitHub',
    Storage__Owner: owner,
    Storage__Repository: repository,
    Storage__Token: token,
    Storage__Branch: branch,
    Storage__RootPath: rootPath,
    Storage__ApiBaseUrl: apiBaseUrl,
  };

  const child = childProcess.spawn(
    'dotnet',
    ['run', '--project', serverProject, '--no-launch-profile', '--urls', url],
    {
      cwd: actionPath,
      detached: true,
      env,
      stdio: ['ignore', out, out],
    });

  child.unref();

  saveState('bagetter_pid', String(child.pid));
  saveState('bagetter_log', logFile);

  console.log(`Started BaGetter with PID ${child.pid}`);
  await waitForServer(child.pid, sourceUrl, logFile);

  setOutput('source-url', sourceUrl);
  setOutput('api-key', apiKey);
  console.log(`BaGetter is ready at ${sourceUrl}`);
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
