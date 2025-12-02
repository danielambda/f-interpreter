;; requires prelude.lisp

(func left  (x) (pair 'left  x))
(func right (x) (pair 'right x))

(func either! (on-left on-right)
  (lambda (e)
    ((cond (equal 'left  (fst e)) on-left
     (cond (equal 'right (fst e)) on-right))
    (snd e))))

(func bimap (f g)
  (either! (compose left f) (compose right g)))

(func mapLeft (f)
  (bimap f id))

(func mapRight (g)
  (bimap id g))

((mapRight ((curry plus) 5)) (left 'not-right))
((mapRight ((curry plus) 5)) (right 50))

(setq flatRight
  (either! left id))

(flatRight (right (right 52)))
(flatRight (right (left 'err)))
(flatRight (left 'err))

(func flatMapRight (f)
  (compose flatRight (mapRight f)))

(func bind (e f)
  ((flatMapRight f) e))

(bind (right '()) (lambda (a)
  (cond (equal '() a)
    (right 5)
    (left '(expected an empty list)))))

(bind (right '(a)) (lambda (a)
  (cond (equal '() a)
    (right 5)
    (left '(expected an empty list)))))

(bind (left '(some other value)) (lambda (a)
  (cond (equal '() a)
    (right 5)
    (left '(expected an empty list)))))

'f-has-proper-eithers!!!
