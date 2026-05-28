#define AL_ALEXT_PROTOTYPES

#include "AudioEnvironment.h"

#include <AL/al.h>
#include <AL/alc.h>
#include <AL/efx.h>

#include <iostream>

AudioEnvironment::AudioEnvironment()
    : effect(0),
      slot(0),
      ready(false) {}

AudioEnvironment::~AudioEnvironment()
{
    Destroy();
}

bool AudioEnvironment::Init(ReverbPreset preset)
{
    ALCcontext *currentContext = alcGetCurrentContext();

    if (!currentContext)
    {
        std::cerr << "[AudioEnvironment] No current OpenAL context. "
                  << "Call AudioEngine::Init() before creating AudioEnvironment."
                  << std::endl;
        return false;
    }

    ALCdevice *currentDevice = alcGetContextsDevice(currentContext);

    if (!currentDevice)
    {
        std::cerr << "[AudioEnvironment] No current OpenAL device." << std::endl;
        return false;
    }

    // Correct EFX extension check.
    // EFX should be checked through ALC_EXT_EFX on the current device.
    if (!alcIsExtensionPresent(currentDevice, "ALC_EXT_EFX"))
    {
        std::cerr << "[AudioEnvironment] ALC_EXT_EFX is not supported. Reverb disabled." << std::endl;
        return false;
    }

    alGenEffects(1, &effect);

    if (alGetError() != AL_NO_ERROR || effect == 0)
    {
        std::cerr << "[AudioEnvironment] Failed to generate EFX effect." << std::endl;
        effect = 0;
        return false;
    }

    alEffecti(effect, AL_EFFECT_TYPE, AL_EFFECT_REVERB);

    if (alGetError() != AL_NO_ERROR)
    {
        std::cerr << "[AudioEnvironment] Failed to set effect type to AL_EFFECT_REVERB." << std::endl;

        alDeleteEffects(1, &effect);
        effect = 0;

        return false;
    }

    if (!ApplyPreset(preset))
    {
        std::cerr << "[AudioEnvironment] Failed to apply reverb preset." << std::endl;

        alDeleteEffects(1, &effect);
        effect = 0;

        return false;
    }

    alGenAuxiliaryEffectSlots(1, &slot);

    if (alGetError() != AL_NO_ERROR || slot == 0)
    {
        std::cerr << "[AudioEnvironment] Failed to generate auxiliary effect slot." << std::endl;

        alDeleteEffects(1, &effect);
        effect = 0;
        slot = 0;

        return false;
    }

    alAuxiliaryEffectSloti(
        slot,
        AL_EFFECTSLOT_EFFECT,
        static_cast<ALint>(effect));

    if (alGetError() != AL_NO_ERROR)
    {
        std::cerr << "[AudioEnvironment] Failed to attach reverb effect to auxiliary slot." << std::endl;

        alDeleteAuxiliaryEffectSlots(1, &slot);
        alDeleteEffects(1, &effect);

        effect = 0;
        slot = 0;

        return false;
    }

    ready = true;

    std::cout << "[AudioEnvironment] Reverb environment initialized successfully." << std::endl;

    return true;
}

void AudioEnvironment::Destroy()
{
    if (slot != 0)
    {
        alAuxiliaryEffectSloti(
            slot,
            AL_EFFECTSLOT_EFFECT,
            AL_EFFECT_NULL);

        alDeleteAuxiliaryEffectSlots(1, &slot);
        slot = 0;
    }

    if (effect != 0)
    {
        alDeleteEffects(1, &effect);
        effect = 0;
    }

    ready = false;
}

bool AudioEnvironment::ApplyPreset(ReverbPreset preset)
{
    if (effect == 0)
    {
        return false;
    }

    switch (preset)
    {
    case ReverbPreset::None:
        alEffectf(effect, AL_REVERB_DENSITY, 0.0f);
        alEffectf(effect, AL_REVERB_DIFFUSION, 0.0f);
        alEffectf(effect, AL_REVERB_GAIN, 0.0f);
        alEffectf(effect, AL_REVERB_GAINHF, 0.0f);
        alEffectf(effect, AL_REVERB_DECAY_TIME, 0.1f);
        break;

    case ReverbPreset::SmallRoom:
        alEffectf(effect, AL_REVERB_DENSITY, 0.45f);
        alEffectf(effect, AL_REVERB_DIFFUSION, 0.55f);
        alEffectf(effect, AL_REVERB_GAIN, 0.35f);
        alEffectf(effect, AL_REVERB_GAINHF, 0.60f);
        alEffectf(effect, AL_REVERB_DECAY_TIME, 1.10f);
        alEffectf(effect, AL_REVERB_REFLECTIONS_GAIN, 0.30f);
        alEffectf(effect, AL_REVERB_LATE_REVERB_GAIN, 0.75f);
        break;

    case ReverbPreset::Cave:
    {
        alEffectf(effect, AL_REVERB_DENSITY, 0.65f);
        alEffectf(effect, AL_REVERB_DIFFUSION, 0.55f);

        alEffectf(effect, AL_REVERB_GAIN, 0.35f);
        alEffectf(effect, AL_REVERB_GAINHF, 0.45f);

        alEffectf(effect, AL_REVERB_DECAY_TIME, 1.35f);
        alEffectf(effect, AL_REVERB_DECAY_HFRATIO, 0.45f);

        alEffectf(effect, AL_REVERB_REFLECTIONS_GAIN, 0.18f);
        alEffectf(effect, AL_REVERB_REFLECTIONS_DELAY, 0.025f);

        alEffectf(effect, AL_REVERB_LATE_REVERB_GAIN, 0.28f);
        alEffectf(effect, AL_REVERB_LATE_REVERB_DELAY, 0.055f);

        alEffectf(effect, AL_REVERB_AIR_ABSORPTION_GAINHF, 0.92f);

        break;
    }

    case ReverbPreset::ConcertHall:
        alEffectf(effect, AL_REVERB_DENSITY, 0.95f);
        alEffectf(effect, AL_REVERB_DIFFUSION, 0.90f);
        alEffectf(effect, AL_REVERB_GAIN, 0.55f);
        alEffectf(effect, AL_REVERB_GAINHF, 0.70f);
        alEffectf(effect, AL_REVERB_DECAY_TIME, 3.20f);
        alEffectf(effect, AL_REVERB_REFLECTIONS_GAIN, 0.55f);
        alEffectf(effect, AL_REVERB_LATE_REVERB_GAIN, 1.10f);
        break;

    default:
        return false;
    }

    ALenum error = alGetError();

    if (error != AL_NO_ERROR)
    {
        std::cerr << "[AudioEnvironment] OpenAL error while applying preset. Error code: "
                  << error
                  << std::endl;
        return false;
    }

    return true;
}

ALuint AudioEnvironment::GetSlot() const
{
    return slot;
}

bool AudioEnvironment::IsReady() const
{
    return ready;
}