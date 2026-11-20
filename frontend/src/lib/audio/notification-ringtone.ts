/**
 * ==========================================================================================
 * WHAT: Short two-tone ringtone for new low-stock notifications (Web Audio).
 * WHY: Header Notification Center — no binary asset; browser-safe "call" cue.
 * EXAMPLE: playNotificationRingtone() after pending alert count increases.
 * ==========================================================================================
 */

let lastPlayAt = 0;

export function playNotificationRingtone(): void {
  if (typeof window === "undefined") return;

  // Debounce rapid polls (max once / 2.5s).
  const now = Date.now();
  if (now - lastPlayAt < 2500) return;
  lastPlayAt = now;

  try {
    const AudioCtx =
      window.AudioContext ||
      (window as unknown as { webkitAudioContext?: typeof AudioContext })
        .webkitAudioContext;
    if (!AudioCtx) return;

    const ctx = new AudioCtx();
    const master = ctx.createGain();
    master.gain.value = 0.18;
    master.connect(ctx.destination);

    const scheduleTone = (freq: number, start: number, dur: number) => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = "sine";
      osc.frequency.value = freq;
      gain.gain.setValueAtTime(0.0001, start);
      gain.gain.exponentialRampToValueAtTime(0.9, start + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, start + dur);
      osc.connect(gain);
      gain.connect(master);
      osc.start(start);
      osc.stop(start + dur + 0.02);
    };

    const t0 = ctx.currentTime;
    // Classic short "phone" double-ring.
    scheduleTone(880, t0, 0.12);
    scheduleTone(1175, t0 + 0.14, 0.12);
    scheduleTone(880, t0 + 0.4, 0.12);
    scheduleTone(1175, t0 + 0.54, 0.12);

    window.setTimeout(() => {
      void ctx.close();
    }, 1200);
  } catch {
    // Autoplay / AudioContext restrictions — silent fail.
  }
}
