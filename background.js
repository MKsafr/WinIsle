// Meneruskan lirik caption YouTube ke aplikasi WinIsle (localhost, port 47834).
chrome.runtime.onMessage.addListener((msg, sender, send) => {
  if (!msg || msg.type !== 'winisle-lyrics') return;
  fetch('http://127.0.0.1:47834/lyrics', {
    method: 'POST',
    headers: { 'X-WinIsle': 'island', 'Content-Type': 'text/plain' },
    body: msg.body
  })
    .then((r) => send({ ok: r.ok }))
    .catch(() => send({ ok: false }));
  return true;
});
