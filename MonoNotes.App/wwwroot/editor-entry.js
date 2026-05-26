// 引入 Toast UI Editor 核心引擎和原生样式
import Editor from '@toast-ui/editor';
import '@toast-ui/editor/dist/toastui-editor.css';

window.MonoNotesEditor = {
    instances: {}, // 支持多实例缓存

    init: function (elementId, dotNetHelper, initialText) {
        const editor = new Editor({
            el: document.getElementById(elementId),
            initialValue: initialText,
            initialEditType: 'wysiwyg', // 默认进入“所见即所得”富文本模式！
            previewStyle: 'vertical',
            height: '100%',
            hideModeSwitch: true, // 隐藏底部的模式切换，让界面更像 Notion
            events: {
                change: () => {
                    // 当内容发生改变时，将 Markdown 源码传回给 C#
                    dotNetHelper.invokeMethodAsync('UpdateContent', editor.getMarkdown());
                }
            }
        });

        this.instances[elementId] = editor;
    },

    setContent: function (elementId, newText) {
        const editor = this.instances[elementId];
        // 只有当传入的新文本和当前编辑器里的不同时，才重新赋值，防止光标乱跳
        if (editor && editor.getMarkdown() !== newText) {
            editor.setMarkdown(newText);
        }
    }
};