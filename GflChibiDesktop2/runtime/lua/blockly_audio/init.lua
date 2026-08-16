local rl = require("raylib").lib

local M = {}
local audios = {}
local volume = 100 / 100
local init = false

M.audios = audios

M.playing = nil

M.single = false

local function ensureInit()
    if not init then
        init = true
        rl.InitAudioDevice()
    end
end

M.register = function(path)
    ensureInit()
    local audio = {
        path = path,
        audio = rl.LoadSound(path)
    }
    setmetatable(audio, {
        __index = {
            play = function(self)
                if M.single then
                    if M.playing ~= nil then M.playing:stop() end
                    M.playing = self
                end
                rl.PlaySound(self.audio)
            end,
            stop = function(self)
                rl.StopSound(self.audio)
            end,
            loop = function (self, is)
                self.looping = is
            end,
            isplaying = function (self)
                return rl.IsSoundPlaying(self.audio)
            end
        }
    })
    audios[#audios+1] = audio
    return audio
end

local deprecated = false

M.update = function()
    if not deprecated then log("[WARN] blockly_audio.update is deprecated and will be removed soon") end
end

M.volume = function (s_volume)
    ensureInit()
    if s_volume == nil then return math.ceil(volume * 100) end
    volume = s_volume / 100
    rl.SetMasterVolume(volume)
end

return M
