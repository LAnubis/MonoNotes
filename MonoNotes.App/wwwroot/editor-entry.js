import Vditor from 'vditor';
import 'vditor/dist/index.css';

window.MonoNotesEditor = {
    instances: {},

    init: function (elementId, dotNetHelper, initialText) {
        // Vditor 的初始化是异步的，所以我们配置在 after 回调里保存实例
        const editor = new Vditor(elementId, {
            value: initialText || "",
            height: '100%',
            mode: 'ir', // 🌟 核心：'ir' (Instant Rendering) 也就是 Obsidian 的即时渲染模式
            icon: 'ant', // 使用更现代的 Ant Design 图标包
            outline: {
                enable: false, // 默认关闭右侧大纲，可通过菜单栏开启
                position: 'right'
            },
            cache: {
                enable: false // 关闭内置缓存，由我们的 C# 和 Blazor 接管数据存储
            },
            // 🌟 极其丰富的菜单栏配置
            toolbar: [
                'emoji', 'headings', 'bold', 'italic', 'strike', 'link', '|',
                'list', 'ordered-list', 'check', 'outdent', 'indent', '|',
                'quote', 'line', 'code', 'inline-code', 'insert-before', 'insert-after', '|',
                'table', '|',
                'undo', 'redo', '|',
                'edit-mode', // 允许用户在 所见即所得、即时渲染、纯源码 之间切换
                'outline',   // 开启/关闭大纲
                'export'     // 导出为 PDF/HTML 等
            ],
            after: () => {
                this.instances[elementId] = editor;
            },
            input: (value) => {
                // 内容改变时，通知 C# 后端保存
                dotNetHelper.invokeMethodAsync('UpdateContent', value);
            }
        });
    },

    setContent: function (elementId, text) {
        const editor = this.instances[elementId];
        if (editor) {
            // Vditor 使用 setValue 来更新内容
            editor.setValue(text || "", true);
        }
    }
};