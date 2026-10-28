// Meneruskan lirik caption YouTube ke aplikasi VorsWinIsland (localhost, port 47834).
chrome.runtime.onMessage.addListener((msg, sender, send) => {
  if (!msg || msg.type !== 'vors-lyrics') return;
  fetch('http://127.0.0.1:47834/lyrics', {
    method: 'POST',
    headers: { 'X-Vors': 'island', 'Content-Type': 'text/plain' },
    body: msg.body
  })
    .then((r) => send({ ok: r.ok }))
    .catch(() => send({ ok: false }));
  return true;
});
