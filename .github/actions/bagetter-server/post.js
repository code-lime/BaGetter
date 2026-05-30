const fs = require('fs');
const os = require('os');

function state(name) {
  return process.env[`STATE_${name}`] || '';
}

function isRunning(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

async function waitForExit(pid) {
  for (let attempt = 0; attempt < 30; attempt++) {
    if (!isRunning(pid)) {
      return true;
    }

    await new Promise((resolve) => setTimeout(resolve, 1000));
  }

  return false;
}

async function stop() {
  const pidValue = state('bagetter_pid');
  if (!pidValue) {
    console.log('BaGetter PID was not found; nothing to stop');
    return;
  }

  const pid = Number(pidValue);
  if (!Number.isInteger(pid) || pid <= 0) {
    console.log(`BaGetter PID '${pidValue}' is invalid; nothing to stop`);
    return;
  }

  if (!isRunning(pid)) {
    console.log('BaGetter is already stopped');
    return;
  }

  const signalPid = process.platform === 'win32' ? pid : -pid;

  try {
    process.kill(signalPid, 'SIGTERM');
  } catch {
    process.kill(pid, 'SIGTERM');
  }

  if (!await waitForExit(pid)) {
    try {
      process.kill(signalPid, 'SIGKILL');
    } catch {
      try {
        process.kill(pid, 'SIGKILL');
      } catch {
      }
    }
  }

  const logFile = state('bagetter_log');
  if (logFile && fs.existsSync(logFile)) {
    console.log(`BaGetter log: ${logFile}`);
  }

  console.log('Stopped BaGetter');
}

stop().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});
