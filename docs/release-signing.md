# Android 发布签名密钥（keystore）操作手册

> 本文档只写步骤，**不包含任何密钥内容**。密钥文件与口令请放在密码管理器中，绝不提交到仓库。

## 0. 原则

- 密钥文件放在仓库**外**：`%USERPROFILE%\.sukeflow\sukeflow-release.keystore`（`.gitignore` 已额外兜底 `*.keystore` / `*.jks` / `*.b64`）。
- 密钥 + 口令必须备份（密码管理器 / 离线介质）。**丢失后老用户无法覆盖安装新版本**（APK 签名不匹配），只能卸载重装 —— 而卸载会清掉应用私有目录里的 `timetable.json`。
- 别名、口令保持一致：`alias = sukeflow`，keystore 口令 = key 口令（PKCS12）。
- 若将来要上 Google Play，请启用 Play App Signing：此密钥作为 upload key，仍可重置。

## 1. 生成密钥（Git Bash）

```bash
mkdir -p "$HOME/.sukeflow"

keytool -genkeypair -v \
  -keystore "$HOME/.sukeflow/sukeflow-release.keystore" \
  -alias sukeflow \
  -keyalg RSA -keysize 2048 -validity 10000 \
  -storetype PKCS12 \
  -dname "CN=SukeFlow, OU=Mobile, O=SukeFlow, L=Hangzhou, ST=Zhejiang, C=CN"
```

提示输入时：

1. `Enter keystore password:` → 输入一个强口令（记下来，记为 **STORE_PASSWORD**）。
2. `Re-enter new password:` → 再输一次。
3. `Enter key password for <sukeflow>: (RETURN if same as keystore password)` → **直接回车**（让 key 口令 = keystore 口令，记为 **KEY_PASSWORD**，与 STORE_PASSWORD 相同）。

`-validity 10000` ≈ 27 年，满足 Google Play 对 2033 年之后到期的要求。若 `keytool` 不在 PATH，可用 Android Studio 自带的 JBR（`<Android Studio>\jbr\bin\keytool`）或任意 JDK 17+ 的 `keytool`。

## 2. 记录证书指纹（备份用，可选但建议）

```bash
keytool -list -v -keystore "$HOME/.sukeflow/sukeflow-release.keystore" -alias sukeflow
```

记下 `SHA256:` 指纹（未来排查签名不一致、配置 App Links 时会用到）。

## 3. 生成单行 base64（给 CI 用）

Git Bash：

```bash
base64 -w0 "$HOME/.sukeflow/sukeflow-release.keystore" > "$HOME/.sukeflow/keystore.b64"
cat "$HOME/.sukeflow/keystore.b64"
```

PowerShell 等价（直接进剪贴板）：

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("$HOME\.sukeflow\sukeflow-release.keystore")) | Set-Clipboard
```

## 4. 校验 base64 可无损还原

```bash
mkdir -p /tmp/kscheck
base64 -d "$HOME/.sukeflow/keystore.b64" > /tmp/kscheck/roundtrip.keystore
sha256sum "$HOME/.sukeflow/sukeflow-release.keystore" /tmp/kscheck/roundtrip.keystore   # 两行哈希必须相同
rm -rf /tmp/kscheck
```

## 5. 配置 GitHub Secrets

仓库 → `Settings` → `Secrets and variables` → `Actions` → `New repository secret`，共 4 个：

| Secret 名 | 值 |
| --- | --- |
| `ANDROID_KEYSTORE_BASE64` | 第 3 步输出的**整行** base64（无换行、无引号） |
| `ANDROID_KEYSTORE_PASSWORD` | 第 1 步的 STORE_PASSWORD |
| `ANDROID_KEY_ALIAS` | `sukeflow` |
| `ANDROID_KEY_PASSWORD` | 第 1 步的 KEY_PASSWORD（与 STORE_PASSWORD 相同） |

> 若希望用 Environment（例如 `release`）隔离这些密钥，改用 `Settings → Environments → release → Environment secrets`，并在 workflow 里声明 `environment: release`。

## 6. CI 中的对应参数（供 workflow 参考）

```bash
# 由 base64 还原
echo "$ANDROID_KEYSTORE_BASE64" | base64 -d > "$RUNNER_TEMP/sukeflow.keystore"

dotnet publish SukeFlow.Android/SukeFlow.Android.csproj -c Release -f net10.0-android \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore="$RUNNER_TEMP/sukeflow.keystore" \
  -p:AndroidSigningStorePass="$ANDROID_KEYSTORE_PASSWORD" \
  -p:AndroidSigningKeyAlias="$ANDROID_KEY_ALIAS" \
  -p:AndroidSigningKeyPass="$ANDROID_KEY_PASSWORD" \
  -p:ApplicationDisplayVersion="$VERSION" \
  -p:ApplicationVersion="$VERSION_CODE"
```

产物：`SukeFlow.Android/bin/Release/net10.0-android/com.sukeflow.app-Signed.apk`。

## 7. 收尾

- 确认 `git status` 不含 `*.keystore` / `*.b64`。
- 清理临时文件：`rm -f "$HOME/.sukeflow/keystore.b64"`（base64 已上传 Secrets 后即可删除；密钥本体保留备份）。
- 不要为了"方便"把密钥塞进仓库或云端公开位置。
