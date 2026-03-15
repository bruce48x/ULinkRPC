const Hexo = require('hexo');

async function main() {
  const hexo = new Hexo(process.cwd(), {});

  try {
    await hexo.init();
    await hexo.call('clean');
    await hexo.call('generate');
    await hexo.exit();
  } catch (error) {
    await hexo.exit(error);
    process.exitCode = 1;
  }
}

main();
