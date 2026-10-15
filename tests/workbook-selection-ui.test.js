const fs = require('fs');
const source = fs.readFileSync('runner/AutoTest.Web/wwwroot/app.js', 'utf8');
const generated = source.indexOf('state.workbookUploadId=null;state.selectedWorkbookCases.clear()');
if (generated < 0) throw new Error('selection reset missing after generate');
if (!source.includes('hãy phân tích workbook lại')) throw new Error('rerun guidance missing');
if (!source.includes("setAttribute('aria-disabled',state.workbookUploadId?'false':'true')")) throw new Error('disabled state marker missing');
console.log('workbook selection UI self-check: PASS');

if (!source.includes("item.supported&&state.workbookUploadId?'':'disabled'")) throw new Error('checkbox lock missing');

