# Acoustic propagation

`SoundBus.Emit` computes a bounded 3D arrival field when hearing listeners or acoustic observers need one.
`AcousticField` uses fast marching through air cells, so the front bends around obstacles
and travels through openings. Air nodes near geometry have ray-tested connections, so
thin walls block paths without inflating walls and floors by a whole cell. Triggers, character
controllers and the emitter's own colliders are ignored. Set `SoundBus.ObstacleMask`
to exclude decorative or non-acoustic collider layers.

## Hearing

- Arrival is delayed by path length / 343 m/s. `SoundPropagationDriver` advances pending
  sounds automatically during play. `SoundBus.Advance(time)` is available for editor checks.
- Pressure falls approximately as 1/r (energy as 1/r²), with air absorption and additional
  loss on indirect paths. These are game-scaled pressure values, not calibrated decibels.
- Listener sensitivity scales hearing range and received pressure. Silent emissions do
  not notify observers. Listeners receive the original position, timestamp, source and tag,
  with **received** pressure in `Intensity` and acoustic path length in the distance argument.
- `OnSoundEmitted` remains an immediate source notification. `OnSoundPropagated` also
  supplies the field to acoustic observers. The visual scanner uses OnSoundEmitted.
- Unregistering cancels pending deliveries. Regeneration and scene unload clear pending waves.

## Visuals

The visual effect follows the neon scanner specification: a world-space sphere expands
from each sound origin at a readable speed (default 12 m/s). Surface pixels light up
when distance to the origin reaches the sphere radius. This is an outgoing visual front,
independent of the obstacle-aware hearing simulation. Depth testing still hides surfaces
behind visible geometry; there is no rendered sphere mesh or screen-space circle.

Up to 16 spheres run together. Their RGB radiance adds before exposure, so intersecting
waves mix colors. Walking is mint, running amber, voice blue, impacts coral. The softer
palette uses reduced saturation and brightness, with gentler bloom. The profile
palette is editable. Radius is source range times sqrt(loudness), with a bounded loudness
scale, while emission intensity also scales brightness. Silent sounds produce no pulse.

Footsteps have additional visual range/brightness/trail tuning for navigation. Normal
walking reveals about 16 m and keeps a fading surface trail for about 2 seconds;
running reveals about 29 m. The first contact emits when movement begins, then strides
follow distance travelled, with a short anti-spam cooldown. Hearing radius and voice
profiles retain their own tuning. The optional per-profile rangeMultiplier defaults to 1
for older serialized profiles that do not contain it.

The shared shader include uses spherical distance for arrival timing, then separates
the short surface-contact response from the existing fading surface memory. Contact
emphasizes Fresnel, geometric boundaries and subtle world-anchored surface variation,
instead of a broad uniformly glowing sphere shell. The temporal trail keeps its previous
color, range and decay. Upward-facing ground below the camera is dimmed, especially near
the player's feet; walls, props, tabletops and ceilings keep their response. A separable
highlight bloom adds glow.
The camera effect works on the existing scene materials; EcholocationSurface provides
an emissive material version using the same pulse data. Both work without scene lighting.

Tune waveSpeed, waveFrontWidth, trailDecayDuration, surfaceTint (surface fill),
fresnelIntensity and edgeHighlightIntensity in EcholocationConfig. Optional gridIntensity
is zero by default. The camera exposes masterBrightness, revealOriginalTexture (subtle
material detail only) and bloomStrength; zero bloom skips its passes.
The Surface Contact group exposes floorVisibility, nearFloorVisibility, contactIntensity
and contactVariation. Setting both floor values to 1 disables ground suppression;
setting contactIntensity to 0 leaves only the fading surface memory.
This uses the existing Built-in Render Pipeline, not URP.

Attach ImpactSoundEmitter to throwable rigidbody props for automatic collision pulses.
Quiet contacts are ignored, impact speed scales range/intensity, and a cooldown prevents
contact spam. Player footsteps, jumping, landing and V microphone input are connected already.

## Local push to talk

Hold **V** in focused gameplay to capture the default Windows microphone. Release V,
unlock the cursor, pause, disable the player or switch away from the application to stop.
`FirstPersonPlayer` installs `PushToTalkVoice` and assigns the camera as the mouth position.
No microphone is opened before a press. Audio is neither saved nor sent over a network,
and there is no speaker loopback. This is the local voice prototype, not multiplayer chat.

The bottom-left indicator shows connection/listening/speaking state and the input level.
No-device/access errors are shown there; check Windows microphone permissions and input
selection, then release and press V to retry. You can choose an exact device name through
`microphoneDevice` on the component; empty uses the OS default.

The detector processes 20 ms PCM frames, rejects DC and quiet input, uses a hysteretic
noise gate and a short release envelope. `noiseGateDb` defaults to -48 dBFS and
`fullLevelDb` to -18 dBFS. Speech emits at most one sound per 0.35 seconds, ranging from
10 to 26 m according to microphone level. These thresholds may need adjustment for your
microphone gain. No speech recognition is involved.

`PcmFrameCaptured` provides reusable interleaved samples to future local consumers.
Consumers must copy the data during the callback if retaining it. No transport is installed.
`VoiceActivity` can be checked with synthetic PCM without opening a real microphone.

## Limits and checks

Gameplay hearing is a geometric first-arrival approximation, **not a full acoustic pressure solver**.
It does not model phase interference, frequency bands, transmission through materials,
or multiple specular reflection/reverberation. Paths around edges approximate diffraction without
a wavelength model. Geometry is sampled at emission; moving a door affects subsequent
sounds. The field has 41³ cells and a maximum 40 m path budget; narrow openings below a
cell can close conservatively. More sensitive listeners share the larger field, which
reduces spatial resolution. Do not use this as a calibrated acoustics simulator.

The number of visual pulses (16), pending hearing waves (64), volume size and range are
bounded. Visual spheres do not allocate acoustic volumes or query colliders. Hearing
field construction performs physics queries on the main thread; profile many
simultaneous emitters before increasing these limits. No unbounded propagation cache is kept.

Editor menu: **EchoSound → Checks → Validate acoustic propagation**, **Validate sound bus**,
**Validate voice activity**, and **Validate echolocation VFX**. Run checks outside play
mode; acoustic/bus checks reset bus subscriptions.
Batch entry `EchoSound.Editor.Tests.AcousticChecks.RunBatch` checks geometry, arrival delay,
attenuation, unsubscribe, shader errors and produces `Logs/AcousticChecks/echo-preview.png`.

Physics references: [sound pressure distance law](https://www.bksv.com/downloads/svpockethandbook/index.html)
and [geometric acoustic arrival fields](https://arxiv.org/abs/2208.13002).
