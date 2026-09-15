(() => {
  const byId = id => document.getElementById(id);
  const types = [
    ['architecture', 'Kiến trúc hệ thống', 'Thành phần, kết nối và ranh giới'],
    ['workflow', 'Quy trình nghiệp vụ', 'Các bước, nhánh và lane xử lý'],
    ['sequence', 'Trình tự API', 'Thứ tự gọi và phản hồi giữa actor'],
    ['dataflow', 'Luồng dữ liệu', 'Nguồn, biến đổi và nơi tiêu thụ'],
    ['lifecycle', 'Vòng đời trạng thái', 'Trạng thái, sự kiện và chuyển tiếp'],
  ];
  const source = byId('architectureSourcePath');
  const description = byId('architectureDescription');
  const file = byId('architectureDescriptionFile');
  const status = byId('architectureStatus');
  const resultPanel = byId('architectureResult');
  const button = byId('generateArchitecture');
  let guides = [];
  let fileContent = '';
  if (!button) return;

  const renderTypes = () => {
    byId('architectureTypeCards').innerHTML = types.map(([value, title, summary]) => `<button type="button" class="diagram-type-card ${byId('architectureType').value === value ? 'selected' : ''}" data-diagram-type="${value}"><b>${title}</b><small>${summary}</small></button>`).join('');
    document.querySelectorAll('[data-diagram-type]').forEach(card => card.onclick = () => {
      byId('architectureType').value = card.dataset.diagramType;
      renderTypes();
      renderGuideOptions();
    });
  };
  const renderGuideOptions = () => {
    const matching = guides.filter(item => item.type === byId('architectureType').value);
    byId('architectureGuide').innerHTML = matching.map(item => `<option value="${escapeHtml(item.id)}">${escapeHtml(item.title)} — ${escapeHtml(item.question)}</option>`).join('');
    renderGuideDetail();
  };
  const renderGuideDetail = () => {
    const guide = guides.find(item => item.id === byId('architectureGuide').value);
    byId('architectureGuideDetail').innerHTML = guide ? `<b>${escapeHtml(guide.title)}</b><span>${escapeHtml(guide.summary)}</span><small><b>Nên dùng khi:</b> ${escapeHtml(guide.useWhen)}</small><small><b>Không nên dùng khi:</b> ${escapeHtml(guide.avoidWhen)}</small><ul>${guide.include.map(item => `<li>${escapeHtml(item)}</li>`).join('')}</ul>` : 'Chưa có hướng dẫn cho loại sơ đồ này.';
  };
  const selectInputMode = mode => {
    byId('architectureGuidePanel').hidden = mode !== 'guide';
    byId('architectureCustomPanel').hidden = mode !== 'custom';
    byId('architectureFilePanel').hidden = mode !== 'file';
  };
  const loadGuides = async () => {
    try {
      const response = await json('/api/archify/guides');
      guides = response.recipes || [];
      renderGuideOptions();
    } catch (error) {
      byId('architectureGuideDetail').textContent = readableError(error);
    }
  };

  source.value = localStorage.getItem('toolqc.architectureSourcePath') || localStorage.getItem('toolqc.sourcePath') || '';
  source.oninput = () => localStorage.setItem('toolqc.architectureSourcePath', source.value);
  document.querySelectorAll('[name="architectureInputMode"]').forEach(input => input.onchange = () => selectInputMode(input.value));
  byId('architectureGuide').onchange = renderGuideDetail;
  file.onchange = async () => {
    const selected = file.files?.[0];
    if (!selected) return;
    fileContent = await selected.text();
    byId('architectureFileStatus').textContent = `Đã nạp ${selected.name} · ${fileContent.length.toLocaleString()} ký tự.`;
  };
  button.onclick = async () => {
    const mode = document.querySelector('[name="architectureInputMode"]:checked')?.value || 'guide';
    const guide = guides.find(item => item.id === byId('architectureGuide').value);
    const request = mode === 'guide' ? guide?.prompt : mode === 'file' ? fileContent : description.value.trim();
    if (!request) return alert('Hãy chọn hướng dẫn, nhập yêu cầu hoặc chọn file mô tả trước khi vẽ sơ đồ.');
    const started = Date.now();
    button.disabled = true;
    resultPanel.hidden = true;
    status.className = 'architecture-running';
    const progress = setInterval(() => status.textContent = `AI đang đọc source và Archify đang vẽ sơ đồ · ${Math.floor((Date.now() - started) / 1000)} giây`, 1000);
    try {
      const response = await json('/api/archify/generate', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ sourcePath: source.value.trim(), description: request, diagramType: byId('architectureType').value, provider: byId('architectureProvider').value, visualPreset: byId('architecturePreset').value, animation: byId('architectureAnimation').value }) });
      status.className = 'architecture-success';
      status.textContent = response.repairAttempts > 0
        ? `Đã tự sửa ${response.repairAttempts} vòng và kiểm tra sơ đồ thành công sau ${((Date.now() - started) / 1000).toFixed(1)} giây.`
        : `Đã tạo và kiểm tra sơ đồ sau ${((Date.now() - started) / 1000).toFixed(1)} giây.`;
      resultPanel.hidden = false;
      resultPanel.innerHTML = `<b>Sơ đồ đã sẵn sàng</b><a href="${response.url}" target="_blank" rel="noopener">Mở sơ đồ trong tab mới</a><div>HTML: ${escapeHtml(response.htmlPath)}</div><div>Đặc tả: ${escapeHtml(response.specificationPath)}</div>`;
      window.open(response.url, '_blank', 'noopener');
    } catch (error) {
      status.className = 'architecture-error';
      status.textContent = readableError(error);
    } finally {
      clearInterval(progress);
      button.disabled = false;
    }
  };
  renderTypes();
  selectInputMode('guide');
  loadGuides();
})();
