# Eleven Metres

Game sút luân lưu chân thực trên **Unity 6000.3 LTS**, iOS + Android,
render pipeline **URP Forward+**. Làm bởi một người + agent AI.

- Kế hoạch tổng thể: [docs/plan.md](docs/plan.md)
- Backlog kỹ thuật (34 task): [docs/backlog/README.md](docs/backlog/README.md)

## Cấu trúc

```
Assets/_Project/Art|Audio|Code|Settings|Scenes
Assets/_Project/Code/{Ball,Keeper,Shooter,Match,Presentation,UI}   # mỗi cái một asmdef
Assets/_Project/Tests/{EditMode,PlayMode}
tools/build.sh   # build một lệnh: ./tools/build.sh ios | android
```

## Quy ước nhanh

- `.NET Standard 2.1`, dùng `Unity.Mathematics` thay cho `UnityEngine.Vector3` trong logic thuần
- Không `UnityEngine.Random` trong gameplay — chỉ `Unity.Mathematics.Random` có seed
- Mọi tính năng đồ hoạ hỏi `Eleven.Core.DeviceTier`, không tự quyết
- Hiệu năng đo bằng HUD tích hợp (`Eleven.Core.Diagnostics.PerfHud`), quan trọng là **p95** chứ không phải trung bình
