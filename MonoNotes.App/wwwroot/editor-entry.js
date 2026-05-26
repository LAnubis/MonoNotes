import { basicSetup } from "codemirror";
import { EditorState } from "@codemirror/state";
import { EditorView } from "@codemirror/view";
import { markdown } from "@codemirror/lang-markdown";

window.MonoNotesEditor = {
    init: function (element, dotNetHelper, initialText) {
        let updateListener = EditorView.updateListener.of((update) => {
            if (update.docChanged) {
                dotNetHelper.invokeMethodAsync('UpdateContent', update.state.doc.toString());
            }
        });

        let state = EditorState.create({
            doc: initialText,
            extensions: [
                basicSetup,
                markdown(),
                updateListener,
                EditorView.lineWrapping
            ]
        });

        let view = new EditorView({
            state,
            parent: element
        });

        element.cmView = view;
    },

    setContent: function (element, newText) {
        if (element.cmView) {
            const view = element.cmView;
            const currentText = view.state.doc.toString();
            if (currentText !== newText) {
                view.dispatch({
                    changes: { from: 0, to: currentText.length, insert: newText }
                });
            }
        }
    }
};