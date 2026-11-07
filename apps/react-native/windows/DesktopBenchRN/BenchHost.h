#pragma once

// DesktopBench host module: file I/O, ping and exit on the native side (same role as
// Electron's main process / Tauri's Rust commands). Exposed to JS as NativeModules.BenchHost.

#include <chrono>
#include <cstdlib>
#include <fstream>
#include <sstream>
#include <string>
#include <thread>

#include "NativeModules.h"

namespace DesktopBench {

inline int64_t g_startTs = 0;

inline int64_t NowMs() {
  return std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::system_clock::now().time_since_epoch())
      .count();
}

inline std::string Env(const char *name) {
  char *buf = nullptr;
  size_t len = 0;
  std::string value;
  if (_dupenv_s(&buf, &len, name) == 0 && buf) {
    value = buf;
    free(buf);
  }
  return value;
}

inline std::wstring Widen(const std::string &s) {
  return winrt::to_hstring(s).c_str();
}

inline std::wstring DataFile() {
  auto p = Env("BENCH_DATA");
  if (!p.empty()) return Widen(p);
  return Widen(Env("LOCALAPPDATA")) + L"\\DesktopBench\\items.json";
}

REACT_MODULE(BenchHost)
struct BenchHost {
  REACT_SYNC_METHOD(Config, L"config")
  winrt::Microsoft::ReactNative::JSValueObject Config() noexcept {
    auto idle = Env("BENCH_IDLE_MS");
    return winrt::Microsoft::ReactNative::JSValueObject{
        {"startTs", g_startTs},
        {"benchOut", Env("BENCH_OUT")},
        {"idleMs", idle.empty() ? 3000 : std::atoi(idle.c_str())}};
  }

  // Returns "" when the file does not exist.
  REACT_METHOD(LoadText, L"loadText")
  void LoadText(winrt::Microsoft::ReactNative::ReactPromise<std::string> &&result) noexcept {
    std::ifstream f(DataFile(), std::ios::binary);
    if (!f) {
      result.Resolve("");
      return;
    }
    std::ostringstream ss;
    ss << f.rdbuf();
    result.Resolve(ss.str());
  }

  REACT_METHOD(SaveText, L"saveText")
  void SaveText(std::string text, winrt::Microsoft::ReactNative::ReactPromise<void> &&result) noexcept {
    std::ofstream(DataFile(), std::ios::binary | std::ios::trunc) << text;
    result.Resolve();
  }

  REACT_METHOD(WriteFile, L"writeFile")
  void WriteFile(std::string path, std::string content, winrt::Microsoft::ReactNative::ReactPromise<void> &&result) noexcept {
    std::ofstream(Widen(path), std::ios::binary | std::ios::trunc) << content;
    result.Resolve();
  }

  REACT_METHOD(Ping, L"ping")
  void Ping(int n, winrt::Microsoft::ReactNative::ReactPromise<int> &&result) noexcept {
    result.Resolve(n + 1);
  }

  REACT_METHOD(Exit, L"exit")
  void Exit() noexcept {
    std::thread([] {
      std::this_thread::sleep_for(std::chrono::milliseconds(50));
      ExitProcess(0);
    }).detach();
  }
};

} // namespace DesktopBench
