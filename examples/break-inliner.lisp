(func add (x)
  (lambda (y)
    (plus x y)))

;; this one does NOT inline call to add
(prog ()
  (setq y 100)
  ((add y) 1))

;; bad inlining could lead to
;; (prog ()
;;   (setq y 100)
;;   ((lambda (y) (plus y y)) 1))

;; this one inlines call to add
(prog ()
  (setq y 100)
  ((add 1) y))

