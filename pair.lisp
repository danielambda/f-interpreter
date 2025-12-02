;; requires prelude.lisp
;; requires list.lisp

(func pair (x y) (cons x (singleton y)))
(setq fst head)
(setq snd (compose head tail))
(func pair? (p) (and (islist p) (equal (2 (length p)))))
