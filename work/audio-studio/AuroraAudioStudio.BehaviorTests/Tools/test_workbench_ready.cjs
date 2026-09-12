// Test the exact C#-emitted predicate. This is a DOM fixture, not browser acceptance.
const vm = require('node:vm');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const script = fs.readFileSync(process.argv[2], 'utf8');
function fixture({ identity = 'this-launch', button = true, disabled = false, input = true, output = true, visible = true } = {}) {
  const element = (show = true) => ({ getClientRects: () => show ? [{}] : [], disabled: false });
  const b = { ...element(), disabled, matches: () => true };
  const root = {
    querySelector: () => null,
    querySelectorAll: () => input ? [element(visible)] : [],
    getElementById: id => id === 'component-2' ? (button ? b : null) : (output ? element() : null)
  };
  return {
    window: { gradio_config: { aurora_instance: identity,
      components: [{ id: 2, type: 'button' }, { id: 3, type: 'audio' }],
      dependencies: [{ backend_fn: true, inputs: [1], outputs: [3], targets: [[2, 'click']] }] } },
    document: root, getComputedStyle: () => ({ visibility: 'visible' })
  };
}
assert.equal(vm.runInNewContext(script, fixture()), true);
for (const options of [{ identity: 'old' }, { button: false }, { disabled: true }, { input: false }, { output: false }, { visible: false }]) {
  assert.equal(vm.runInNewContext(script, fixture(options)), false, JSON.stringify(options));
}
console.log('PASS workbench DOM predicate: 7 cases (fixture only)');
