(() => {
  const byId = id => document.getElementById(id);
  const generatorMode = document.querySelector('.generator-mode');
  const analysisOptions = document.querySelector('.analysis-options');
  if (generatorMode && analysisOptions) analysisOptions.after(generatorMode);

  const providerStatus = byId('generatorProvider');
  if (generatorMode && providerStatus && !byId('draftRepairMode')) {
    const repairModeLabel = document.createElement('label');
    repairModeLabel.innerHTML = 'Xử lý lỗi testcase<select id="draftRepairMode"><option value="auto">Tự sửa bằng AI và kiểm tra lại</option><option value="manual">Chỉ báo lỗi để sửa thủ công</option></select>';
    generatorMode.insertBefore(repairModeLabel, providerStatus);
  }

  const toolbar = document.querySelector('#draftEditor .toolbar-buttons');
  const runButton = byId('runDraft');
  if (!toolbar || !runButton || byId('repairDraft')) return;

  const repairButton = document.createElement('button');
  repairButton.id = 'repairDraft';
  repairButton.type = 'button';
  repairButton.className = 'compact repair';
  repairButton.textContent = 'Tự sửa và kiểm tra lại';
  repairButton.hidden = true;
  toolbar.insertBefore(repairButton, runButton);

  const status = byId('draftStatus');
  const observer = new MutationObserver(() => {
    const isPreflightError = status.classList.contains('failed')
      && (status.textContent.includes('TQC-GEN-005') || status.textContent.includes('preflight'));
    repairButton.hidden = !isPreflightError;
  });
  observer.observe(status, { attributes: true, childList: true, characterData: true, subtree: true });

  window.repairGeneratedDraft = async ({ automatic = false } = {}) => {
    if (!state.generatorReviewId) return;
    const started = Date.now();
    repairButton.disabled = true;
    byId('preflightDraft').disabled = true;
    status.className = 'message-summary checking';
    const progress = setInterval(() => {
      const seconds = Math.floor((Date.now() - started) / 1000);
      status.textContent = `⏳ AI đang sửa testcase và tự chạy preflight nhiều vòng · ${seconds} giây\nTool chỉ sửa lỗi testcase, không thay đổi backend.`;
    }, 1000);

    try {
      const result = await json(`/api/projects/${encodeURIComponent(byId('project').value)}/generate/repair`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          reviewId: state.generatorReviewId,
          json: byId('draftJson').value,
          sourcePath: byId('sourcePath').value.trim(),
          provider: byId('generatorAiProvider').value,
          maxAttempts: 5,
        }),
      });
      byId('draftJson').value = result.json;
      state.generatorPreflightPassed = true;
      state.generatorReadyToSave = result.readyToSave;
      runButton.disabled = false;
      byId('activateDraft').disabled = !result.readyToSave;
      repairButton.hidden = true;
      status.className = result.readyToSave ? 'message-summary passed' : 'message-summary warning';
      status.textContent = result.readyToSave
        ? `✓ AI đã sửa và preflight đạt sau ${result.attempts} vòng. Có thể chạy thử và lưu testcase.`
        : `✓ AI đã sửa và preflight chạy được sau ${result.attempts} vòng. Còn ${result.warnings.length} coverage cần xác minh trước khi lưu.`;
    } catch (error) {
      if (error.payload?.json) byId('draftJson').value = error.payload.json;
      repairButton.hidden = false;
      status.className = 'message-summary failed';
      status.textContent = `✕ ${automatic ? 'Tự động sửa' : 'Tự sửa'} chưa hoàn tất.\n\n${readableError(error)}`;
    } finally {
      clearInterval(progress);
      repairButton.disabled = false;
      byId('preflightDraft').disabled = false;
    }
  };
  repairButton.onclick = () => window.repairGeneratedDraft();
})();
